using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Webhooks;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Engine;

/// <summary>
/// Drains the webhook outbox. The sixth background service, and kept separate from the notification dispatcher
/// for the reason all of them are separate: a different cadence and a different failure mode, and nothing may
/// ever be able to take the occurrence engine down with it.
///
/// Every delivery row is independent, which is the failure isolation: a dead receiver cannot delay another
/// receiver, another event, or any request path. The worst a broken subscriber achieves is a row marked failed
/// and a banner for an administrator.
///
/// Registered as a singleton as well as a hosted service so tests drive one pass by hand instead of racing a
/// timer — exactly as the notification dispatcher already is.
/// </summary>
public sealed class WebhookDispatcherService(
    IServiceScopeFactory scopeFactory,
    ITenantContext tenantContext,
    IOptions<WebhookOptions> options,
    ILogger<WebhookDispatcherService> logger) : BackgroundService
{
    /// <summary>In memory on purpose: a sweep missed over a restart costs nothing and catches up.</summary>
    private DateTimeOffset? _lastSweep;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Webhook delivery is disabled (Webhooks:Enabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.DispatchSeconds));

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>One pass. Returns how many deliveries were accepted by their receiver.</summary>
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
            logger.LogError(e, "Webhook dispatch failed.");
            return 0;
        }
    }

    private async Task<int> DispatchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<EverdueDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var sender = services.GetRequiredService<WebhookSender>();

        var now = clock.UtcNow;

        var pending = await db.WebhookDeliveries
            .Include(d => d.Subscription)
            .Where(d => d.Status == DeliveryStatus.Pending && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        var sent = 0;

        foreach (var delivery in pending)
        {
            var subscription = delivery.Subscription;

            if (subscription is null)
            {
                Finish(delivery, DeliveryStatus.Skipped, "The subscription no longer exists.", null, clock.UtcNow);
                continue;
            }

            if (!subscription.Active)
            {
                // Disabled while this row was waiting. Skipped, not failed: nothing was owed.
                Finish(delivery, DeliveryStatus.Skipped, "The subscription is disabled.", null, clock.UtcNow);
                continue;
            }

            var result = await sender.SendAsync(subscription, delivery, cancellationToken);

            Apply(delivery, subscription, result, clock.UtcNow);

            if (result.Sent)
            {
                sent++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return sent;
    }

    private void Apply(WebhookDelivery delivery, WebhookSubscription subscription, WebhookSendResult result, DateTimeOffset now)
    {
        if (result.Sent)
        {
            Finish(delivery, DeliveryStatus.Sent, null, result.StatusCode, now);

            subscription.ConsecutiveFailures = 0;
            subscription.LastSuccessAt = now;
            subscription.LastError = null;
            return;
        }

        delivery.Attempts++;
        delivery.LastError = result.Error;
        delivery.ResponseStatus = result.StatusCode;

        subscription.LastError = result.Error;
        subscription.ConsecutiveFailures++;

        if (subscription.ConsecutiveFailures >= options.Value.MaxConsecutiveFailures && subscription.Active)
        {
            // An endpoint that has failed this many times running has changed. It comes back when somebody
            // says so, not on its own.
            subscription.Active = false;
            subscription.DisabledAt = now;

            logger.LogWarning(
                "Webhook subscription {SubscriptionId} disabled after {Failures} consecutive failures: {Error}",
                subscription.Id,
                subscription.ConsecutiveFailures,
                result.Error);
        }

        if (!result.Retryable || delivery.Attempts >= options.Value.MaxAttempts)
        {
            Finish(delivery, DeliveryStatus.Failed, result.Error, result.StatusCode, now);
            return;
        }

        // The same exponential-capped-at-an-hour curve the notification outbox uses. One definition of "back
        // off", two outboxes.
        delivery.NextAttemptAt = now + NotificationDelivery.BackoffFor(delivery.Attempts);
    }

    private static void Finish(WebhookDelivery delivery, DeliveryStatus status, string? error, int? statusCode, DateTimeOffset now)
    {
        delivery.Status = status;
        delivery.LastError = error;
        delivery.ResponseStatus = statusCode;
        delivery.SentAt = status == DeliveryStatus.Sent ? now : null;
    }

    /// <summary>
    /// Once a day. Succeeded and skipped rows go after the retention window; <strong>failed rows are kept four
    /// times as long</strong>, because they are what an administrator debugs with. Deliveries are not the
    /// ledger — <c>WorkItemEvents</c> is, and it is never swept.
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

        var terminalCutoff = now.AddDays(-options.Value.RetentionDays);
        var failedCutoff = now.AddDays(-options.Value.RetentionDays * 4);

        var stale = await db.WebhookDeliveries
            .Where(d => (d.Status == DeliveryStatus.Sent || d.Status == DeliveryStatus.Skipped) && d.NextAttemptAt < terminalCutoff
                        || d.Status == DeliveryStatus.Failed && d.NextAttemptAt < failedCutoff)
            .Take(1000)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return;
        }

        db.WebhookDeliveries.RemoveRange(stale);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Swept {Count} webhook delivery row(s).", stale.Count);
    }
}
