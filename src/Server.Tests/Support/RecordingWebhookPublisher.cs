using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;

namespace Everdue.Server.Tests.Support;

/// <summary>
/// Records what would have been published, without touching a database.
///
/// A recorder rather than a no-op because the flood guards are behaviour worth asserting: after a fortnight of
/// downtime the ledger must still hold every miss while only the recent ones are announced, and the only honest
/// way to test that is to count what was handed over.
/// </summary>
public sealed class RecordingWebhookPublisher : IWebhookPublisher
{
    public List<(WebhookEventType Type, Guid WorkItemId, bool Late)> WorkItems { get; } = [];

    public List<(WebhookEventType Type, Guid EntityId)> Entities { get; } = [];

    public Task PublishWorkItemAsync(
        WebhookEventType type,
        WorkItem item,
        CancellationToken cancellationToken,
        bool late = false)
    {
        WorkItems.Add((type, item.Id, late));
        return Task.CompletedTask;
    }

    public Task PublishEntityAsync(WebhookEventType type, Entity entity, CancellationToken cancellationToken)
    {
        Entities.Add((type, entity.Id));
        return Task.CompletedTask;
    }

    public IReadOnlyList<(WebhookEventType Type, Guid WorkItemId, bool Late)> Of(WebhookEventType type)
        => WorkItems.Where(w => w.Type == type).ToArray();

    public void Clear()
    {
        WorkItems.Clear();
        Entities.Clear();
    }
}
