using Everdue.Server.Domain;

namespace Everdue.Server.Application.Abstractions;

/// <summary>
/// Turns something that happened into pending webhook deliveries. Enqueue only — how a delivery
/// actually leaves the machine is Infrastructure's business, and the Application layer never learns it.
///
/// Delivery rows are added to the change tracker and committed by the caller's own
/// <c>SaveChangesAsync</c>, so a delivery can never exist for a change that rolled back. The occurrence
/// engine is the one exception and says so at its call site.
/// </summary>
public interface IWebhookPublisher
{
    /// <summary>
    /// Queues one work-item event for every active subscription that asked for its type. Does nothing
    /// when nobody is subscribed, which is the normal case.
    /// </summary>
    Task PublishWorkItemAsync(
        WebhookEventType type,
        WorkItem item,
        CancellationToken cancellationToken,
        bool late = false);

    Task PublishEntityAsync(WebhookEventType type, Entity entity, CancellationToken cancellationToken);
}
