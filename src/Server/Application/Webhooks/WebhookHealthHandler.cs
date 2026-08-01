using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Webhooks;

/// <summary>
/// Health derived from the deliveries table — no counter column, because the rows already hold the answer and a
/// counter is a second copy of a fact that can drift. Exactly how channel health works.
/// </summary>
public sealed class WebhookHealthHandler(IEverdueDbContext db, IClock clock)
    : IRequestHandler<WebhookHealthQuery, IReadOnlyList<WebhookHealthDto>>
{
    public async Task<IReadOnlyList<WebhookHealthDto>> Handle(
        WebhookHealthQuery request,
        CancellationToken cancellationToken = default)
    {
        var since = clock.UtcNow.AddHours(-24);

        var subscriptions = await db.WebhookSubscriptions.AsNoTracking()
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return [];
        }

        var counts = await db.WebhookDeliveries.AsNoTracking()
            .GroupBy(d => d.SubscriptionId)
            .Select(g => new
            {
                SubscriptionId = g.Key,
                Pending = g.Count(d => d.Status == DeliveryStatus.Pending),
                Failed24h = g.Count(d => d.Status == DeliveryStatus.Failed && d.NextAttemptAt >= since),
                Sent24h = g.Count(d => d.Status == DeliveryStatus.Sent && d.SentAt >= since),
            })
            .ToDictionaryAsync(g => g.SubscriptionId, cancellationToken);

        return subscriptions
            .Select(s => new WebhookHealthDto(
                s.Id,
                s.Url,
                s.Active,
                counts.TryGetValue(s.Id, out var c) ? c.Pending : 0,
                counts.TryGetValue(s.Id, out var f) ? f.Failed24h : 0,
                counts.TryGetValue(s.Id, out var t) ? t.Sent24h : 0,
                s.LastSuccessAt,
                s.LastError))
            .ToArray();
    }
}
