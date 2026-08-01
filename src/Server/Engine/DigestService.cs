using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Localization;
using Everdue.Server.Application.Notifications;
using Everdue.Server.Domain;
using Everdue.Server.Engine.Digest;
using Everdue.Server.Infrastructure.Options;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Engine;

/// <summary>
/// The manager surface: one e-mail per subscriber, at the tenant's local digest hour.
///
/// It stays e-mail only. The digest is five sections of tabular summary, and squeezing that into a
/// chat message produces something nobody reads — per-event notifications are what reach phones.
/// Delivery goes through the Email *channel*, so a tenant's own SMTP applies to it automatically.
/// </summary>
public sealed class DigestService(
    IServiceScopeFactory scopeFactory,
    ITenantContext tenantContext,
    IOptions<DigestOptions> options,
    ILogger<DigestService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Daily digest is disabled (Digest:Enabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.CheckMinutes));

        do
        {
            await RunIfDueAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RunIfDueAsync(CancellationToken cancellationToken)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            await new DigestRun(scope.ServiceProvider, logger).ExecuteAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception e)
        {
            logger.LogError(e, "Daily digest run failed.");
        }
    }
}

/// <summary>
/// One pass over the subscribers. A class of its own so the service above stays a timer and nothing
/// else — the scheduling and the work have different reasons to change.
/// </summary>
internal sealed class DigestRun(IServiceProvider services, ILogger logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<EverdueDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var tenantContext = services.GetRequiredService<ITenantContext>();

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null || !tenant.Active)
        {
            return;
        }

        var now = clock.UtcNow;
        var timeZone = tenant.ResolveTimeZone();
        var localNow = TenantTime.LocalDateTime(now, timeZone);
        var localDate = DateOnly.FromDateTime(localNow);

        if (localNow.Hour < tenant.DigestHourLocal)
        {
            return;
        }

        var selector = new DigestSubscriptionSelector(db, services.GetRequiredService<IUserDirectory>());
        var due = await selector.SelectDueAsync(localDate, cancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        var email = services.GetRequiredService<IChannelRegistry>().Find(NotificationChannel.Email);
        if (email is null)
        {
            return;
        }

        var builder = new DigestBuilder(db, services.GetRequiredService<IUserDirectory>(), services.GetRequiredService<ISender>());

        // Subscribers who want the same thing get the same content built once: a fifteen-person
        // company is mostly one org-wide daily digest.
        var contentCache = new Dictionary<(DigestFrequency, Guid?), DigestContent>();
        var sent = 0;

        foreach (var subscriber in due)
        {
            var key = (subscriber.Frequency, subscriber.DepartmentId);

            if (!contentCache.TryGetValue(key, out var content))
            {
                content = await builder.BuildAsync(tenant, now, subscriber.Frequency, subscriber.DepartmentId, cancellationToken);
                contentCache[key] = content;
            }

            var language = Languages.Resolve(subscriber.User.PreferredLanguage, tenant.DefaultLanguage);

            var result = await email.SendAsync(
                new ChannelRecipient(subscriber.User.Id, subscriber.User.DisplayName, subscriber.User.Email, null, null, language),
                new ChannelMessage(
                    DigestTemplates.Subject(content, language),
                    PlainTextFallback(content, language),
                    DigestTemplates.RenderHtml(content, language),
                    Language: language),
                cancellationToken);

            if (result.Outcome == ChannelSendOutcome.Skipped)
            {
                // No SMTP anywhere: nothing is owed and nothing is broken. Say it once, not per person.
                logger.LogInformation("Digest skipped: {Reason}", result.Error);
                return;
            }

            // Recorded even on a failed send: a digest that bounced is not worth re-sending all day,
            // and tomorrow's will contain everything today's would have.
            await selector.MarkSentAsync(subscriber, localDate, cancellationToken);
            sent++;
        }

        logger.LogInformation("Digest sent to {Count} subscriber(s).", sent);
    }

    /// <summary>Text part for mail clients that refuse HTML. Deliberately a summary, not a re-render.</summary>
    private static string PlainTextFallback(DigestContent content, string language)
        => string.Join(
            '\n',
            $"{content.TenantName} — {content.LocalDate:yyyy-MM-dd}",
            $"{DigestTemplates.Section(language, DigestText.WentMissed)}: {content.WentMissed.Count}",
            $"{DigestTemplates.Section(language, DigestText.DueToday)}: {content.DueToday.Count}",
            $"{DigestTemplates.Section(language, DigestText.OnHold)}: {content.OnHold.Sum(g => g.Count)}");
}
