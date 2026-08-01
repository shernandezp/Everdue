using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Webhooks;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Infrastructure.Webhooks;

/// <summary>
/// Fans one event out into pending delivery rows — one per active subscription that asked for its type.
///
/// It <strong>adds</strong> rows and never saves: the caller's own <c>SaveChangesAsync</c> commits them, so a
/// delivery cannot exist for a change that rolled back. The occurrence engine is the one caller that saves
/// separately, and it says so at its call site for a reason the ledger justifies.
///
/// When nobody is subscribed this costs one cached read and does nothing, which is the normal case for most
/// installs and the reason there is no configuration switch to "turn webhooks off".
/// </summary>
public sealed class WebhookPublisher(
    IEverdueDbContext db,
    IClock clock,
    IOptions<WebhookOptions> options,
    ILogger<WebhookPublisher> logger) : IWebhookPublisher
{
    /// <summary>
    /// Cached for the lifetime of the scope. One request can raise several events (a bulk complete raises
    /// thirty), and re-reading a table of at most ten rows for each of them is pure waste.
    /// </summary>
    private List<WebhookSubscription>? _subscriptions;

    /// <summary>Entity names, resolved once per scope. Most callers load the item without its navigation.</summary>
    private readonly Dictionary<Guid, string?> _entityNames = [];

    public async Task PublishWorkItemAsync(
        WebhookEventType type,
        WorkItem item,
        CancellationToken cancellationToken,
        bool late = false)
    {
        var targets = await TargetsForAsync(type, cancellationToken);

        if (targets.Count == 0)
        {
            return;
        }

        var entityName = await EntityNameForAsync(item, cancellationToken);

        foreach (var subscription in targets)
        {
            var eventId = Guid.CreateVersion7();

            Enqueue(
                subscription,
                type,
                eventId,
                WebhookPayloads.ForWorkItem(type, eventId, item, entityName, clock.UtcNow, late));
        }
    }

    /// <summary>
    /// The payload carries the entity's name so a subscriber does not have to call back for it — but the mutators
    /// load a work item without its navigations, so it is looked up here rather than depending on how the caller
    /// happened to fetch the row. Only when somebody is actually subscribed, and only once per entity per scope.
    /// </summary>
    private async Task<string?> EntityNameForAsync(WorkItem item, CancellationToken cancellationToken)
    {
        if (item.Entity is { } loaded)
        {
            return loaded.Name;
        }

        if (item.EntityId is not { } entityId)
        {
            return null;
        }

        if (_entityNames.TryGetValue(entityId, out var cached))
        {
            return cached;
        }

        var name = await db.Entities.AsNoTracking()
            .Where(entity => entity.Id == entityId)
            .Select(entity => entity.Name)
            .FirstOrDefaultAsync(cancellationToken);

        _entityNames[entityId] = name;
        return name;
    }

    public async Task PublishEntityAsync(WebhookEventType type, Entity entity, CancellationToken cancellationToken)
    {
        var targets = await TargetsForAsync(type, cancellationToken);

        foreach (var subscription in targets)
        {
            var eventId = Guid.CreateVersion7();
            Enqueue(subscription, type, eventId, WebhookPayloads.ForEntity(type, eventId, entity, clock.UtcNow));
        }
    }

    /// <summary>Used by the admin test button, which targets one subscription rather than fanning out.</summary>
    public void EnqueuePing(WebhookSubscription subscription)
    {
        var eventId = Guid.CreateVersion7();
        Enqueue(subscription, WebhookEventType.Ping, eventId, WebhookPayloads.Ping(eventId, clock.UtcNow));
    }

    private async Task<List<WebhookSubscription>> TargetsForAsync(WebhookEventType type, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return [];
        }

        _subscriptions ??= await db.WebhookSubscriptions.AsNoTracking()
            .Where(s => s.Active)
            .ToListAsync(cancellationToken);

        return _subscriptions.Where(s => s.WantsEvent(type)).ToList();
    }

    private void Enqueue(WebhookSubscription subscription, WebhookEventType type, Guid eventId, string payload)
    {
        // The payload column is bounded; a payload that does not fit is a bug in the shape, not something to
        // truncate silently into an unparseable body.
        if (payload.Length > 4000)
        {
            logger.LogError(
                "Webhook payload for {EventType} exceeded the column limit and was not queued for subscription {SubscriptionId}.",
                type,
                subscription.Id);

            return;
        }

        db.WebhookDeliveries.Add(new WebhookDelivery
        {
            Id = Guid.CreateVersion7(),
            TenantId = subscription.TenantId,
            SubscriptionId = subscription.Id,
            EventType = type,
            EventId = eventId,
            PayloadJson = payload,
            Status = DeliveryStatus.Pending,
            NextAttemptAt = clock.UtcNow,
        });
    }
}
