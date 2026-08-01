using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Notifications;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Options;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Engine;

/// <summary>
/// Drains the outbox.
///
/// Every delivery row is independent, which is the failure isolation: a dead channel cannot delay
/// another channel, another person, or any request path. Nothing here can affect the ledger — the
/// worst a broken provider achieves is a row marked failed and a banner for an administrator.
/// </summary>
public sealed class NotificationDispatcherService(
    IServiceScopeFactory scopeFactory,
    ITenantContext tenantContext,
    IOptions<NotificationOptions> options,
    ILogger<NotificationDispatcherService> logger) : BackgroundService
{
    /// <summary>
    /// Well inside Telegram's 30/second global cap and its 1/second per chat limit, which at fifteen
    /// to thirty users needs no per-chat bookkeeping at all.
    /// </summary>
    private static readonly TimeSpan BetweenSends = TimeSpan.FromMilliseconds(200);

    /// <summary>In memory on purpose: a sweep missed over a restart costs nothing and catches up.</summary>
    private DateTimeOffset? _lastSweep;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Notification delivery is disabled (Notifications:Enabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.DispatchSeconds));

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
            var sent = await DispatchAsync(scope.ServiceProvider, cancellationToken);
            await SweepAsync(scope.ServiceProvider, cancellationToken);
            return sent;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Notification dispatch failed.");
            return 0;
        }
    }

    private async Task<int> DispatchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<EverdueDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var registry = services.GetRequiredService<IChannelRegistry>();
        var recipients = services.GetRequiredService<INotificationRecipients>();
        var publicBaseUrl = services.GetRequiredService<IOptions<AppOptions>>().Value.PublicBaseUrl;

        var now = clock.UtcNow;

        var pending = await db.NotificationDeliveries
            .Include(d => d.Notification)
            .Where(d => d.Status == DeliveryStatus.Pending && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        var people = await recipients.MapAsync(
            pending.Select(d => d.Notification!.UserId).Distinct(),
            cancellationToken);

        var sent = 0;

        foreach (var delivery in pending)
        {
            var notification = delivery.Notification!;

            if (!people.TryGetValue(notification.UserId, out var person))
            {
                Finish(delivery, DeliveryStatus.Skipped, "The recipient no longer exists.", clock.UtcNow);
                continue;
            }

            var channel = registry.Find(delivery.Channel);
            if (channel is null)
            {
                Finish(delivery, DeliveryStatus.Skipped, $"No implementation for channel {delivery.Channel}.", clock.UtcNow);
                continue;
            }

            var message = NotificationTemplates.Render(notification, person.Language, publicBaseUrl);
            var result = await channel.SendAsync(person.ToChannelRecipient(), message, cancellationToken);

            Apply(delivery, result, clock.UtcNow);

            if (result.Outcome == ChannelSendOutcome.Sent)
            {
                sent++;
            }

            await Task.Delay(BetweenSends, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return sent;
    }

    private void Apply(NotificationDelivery delivery, ChannelSendResult result, DateTimeOffset now)
    {
        switch (result.Outcome)
        {
            case ChannelSendOutcome.Sent:
                Finish(delivery, DeliveryStatus.Sent, null, now);
                break;

            case ChannelSendOutcome.Skipped:
                // Nothing was owed: an unconfigured channel is not a failure, and saying so keeps the
                // health screen honest about what is actually broken.
                Finish(delivery, DeliveryStatus.Skipped, result.Error, now);
                break;

            case ChannelSendOutcome.PermanentFailure:
                delivery.Attempts++;
                Finish(delivery, DeliveryStatus.Failed, result.Error, now);
                break;

            default:
                delivery.Attempts++;
                delivery.LastError = result.Error;

                if (delivery.Attempts >= options.Value.MaxAttempts)
                {
                    Finish(delivery, DeliveryStatus.Failed, result.Error, now);
                    break;
                }

                delivery.NextAttemptAt = now + (result.RetryAfter ?? NotificationDelivery.BackoffFor(delivery.Attempts));
                break;
        }
    }

    private static void Finish(NotificationDelivery delivery, DeliveryStatus status, string? error, DateTimeOffset now)
    {
        delivery.Status = status;
        delivery.LastError = error;
        delivery.SentAt = status == DeliveryStatus.Sent ? now : null;
    }

    /// <summary>
    /// Notifications are not the ledger — WorkItemEvents is, and it is never swept. A **read**
    /// message about last quarter's task has no value to anyone, so it goes.
    ///
    /// Unread rows are kept whatever their age: somebody who has been away for three months should
    /// come back to the things nobody told them about, not to an empty bell. Delivery rows go with
    /// their notification through the cascade.
    ///
    /// Once a day, not once a dispatch pass: a delete scan every thirty seconds would be work done
    /// 2,880 times to find what changes once.
    /// </summary>
    private async Task SweepAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var clock = services.GetRequiredService<IClock>();
        var now = clock.UtcNow;

        if (_lastSweep is { } last && now - last < TimeSpan.FromDays(1))
        {
            return;
        }

        _lastSweep = now;

        var db = services.GetRequiredService<EverdueDbContext>();
        var cutoff = now.AddDays(-options.Value.RetentionDays);

        var stale = await db.Notifications
            .Where(n => n.ReadAt != null && n.CreatedAt < cutoff)
            .Take(500)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return;
        }

        db.Notifications.RemoveRange(stale);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Swept {Count} read notification(s) older than {Days} days.",
            stale.Count,
            options.Value.RetentionDays);
    }
}
