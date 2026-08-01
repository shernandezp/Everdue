using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.WorkItems;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Options;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Engine;

/// <summary>
/// "You have work due today", once, at the hour the tenant chose.
///
/// Kept apart from the occurrence engine on purpose: the engine's one job is the ledger, and the
/// ledger must never be able to fail because a reminder did. Idempotency comes from the notification
/// dedupe key rather than a "last run" marker, so a restart mid-morning changes nothing — the same
/// stateless-by-design rule the engine follows.
/// </summary>
public sealed class DueTodayReminderService(
    IServiceScopeFactory scopeFactory,
    ITenantContext tenantContext,
    IOptions<ReminderOptions> options,
    ILogger<DueTodayReminderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Due-today reminders are disabled (Reminders:Enabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.CheckMinutes));

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>One pass. Exposed so tests drive it directly instead of racing a timer.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!tenantContext.IsResolved)
        {
            return 0;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            return await EnqueueAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Due-today reminder run failed.");
            return 0;
        }
    }

    private async Task<int> EnqueueAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<EverdueDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var enqueuer = services.GetRequiredService<INotificationEnqueuer>();
        var tenantId = services.GetRequiredService<ITenantContext>().TenantId;

        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null || !tenant.Active)
        {
            return 0;
        }

        var now = clock.UtcNow;
        var timeZone = tenant.ResolveTimeZone();
        var localNow = TenantTime.LocalDateTime(now, timeZone);
        var today = DateOnly.FromDateTime(localNow);

        if (localNow.Hour < tenant.ReminderHourLocal)
        {
            return 0;
        }

        var dayStart = TenantTime.StartOfDay(today, timeZone);
        var dayEnd = TenantTime.StartOfDay(today.AddDays(1), timeZone);

        var due = await db.WorkItems.AsNoTracking()
            .Where(w => WorkItemQueries.Outstanding.Contains(w.Status) && w.DueDate >= dayStart && w.DueDate < dayEnd)
            .Select(w => new
            {
                w.Id,
                w.Title,
                w.OwnerUserId,
                EntityName = w.EntityId == null ? null : w.Entity!.Name,
            })
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return 0;
        }

        var requests = due
            .Select(item => new NotificationRequest(
                item.OwnerUserId,
                NotificationType.DueToday,
                item.Id,
                Data: NotificationData.For(
                    (NotificationData.Title, item.Title),
                    (NotificationData.Entity, item.EntityName)),
                DedupeKey: $"DueToday:{item.Id}:{today:yyyy-MM-dd}"))
            .ToArray();

        await enqueuer.EnqueueManyAsync(requests, cancellationToken);
        var written = await db.SaveChangesAsync(cancellationToken);

        if (written > 0)
        {
            logger.LogInformation("Due-today reminders enqueued for {Count} item(s).", due.Count);
        }

        return written;
    }
}
