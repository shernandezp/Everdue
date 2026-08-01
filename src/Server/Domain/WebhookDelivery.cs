namespace Everdue.Server.Domain;

/// <summary>
/// One attempt to get one event to one subscriber.
///
/// The same shape <see cref="NotificationDelivery"/> already proved, and it reuses that type's
/// <see cref="DeliveryStatus"/> and <see cref="NotificationDelivery.BackoffFor"/> rather than growing a
/// parallel vocabulary. Independent rows are the failure isolation: a dead subscriber cannot delay
/// another subscriber, another event, or any request path.
/// </summary>
public class WebhookDelivery : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    public Guid SubscriptionId { get; set; }

    public WebhookEventType EventType { get; set; }

    /// <summary>
    /// The value of the <c>webhook-id</c> header. Stable across retries, which is what lets a receiver
    /// deduplicate — delivery is at-least-once and the docs say so.
    /// </summary>
    public Guid EventId { get; set; } = Guid.CreateVersion7();

    public string PayloadJson { get; set; } = string.Empty;

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    /// <summary>The subscriber's HTTP status, when there was one. Null for a timeout or a refused connection.</summary>
    public int? ResponseStatus { get; set; }

    public string? LastError { get; set; }

    public WebhookSubscription? Subscription { get; set; }
}
