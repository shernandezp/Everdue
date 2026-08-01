namespace Everdue.Server.Domain;

/// <summary>
/// Where a tenant wants to be told about work, and which events it cares about.
///
/// Outbound only, deliberately: Everdue makes HTTP calls out, which a home or office router permits,
/// and never needs an inbound endpoint. That is the same decision Telegram's long polling made,
/// for the same audience.
/// </summary>
public class WebhookSubscription : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Data-Protection ciphertext of the signing secret, over the key ring in <c>{DataDir}/keys</c>.
    /// Shown once at creation and never returned by any endpoint again.
    /// </summary>
    public string SecretProtected { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated <see cref="WebhookEventType"/> names. Read in memory — a JSON or array column
    /// would need a predicate neither provider can express portably, and the list has six members.
    /// </summary>
    public string EventTypes { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    /// <summary>
    /// Reset to zero by any success. When it reaches the configured maximum the subscription is
    /// disabled and stays disabled until an administrator says otherwise — an endpoint that has failed
    /// ten times in a row has changed, and guessing otherwise just resumes the noise.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    public DateTimeOffset? DisabledAt { get; set; }

    public DateTimeOffset? LastSuccessAt { get; set; }

    public string? LastError { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public IReadOnlyList<WebhookEventType> SubscribedTypes() => WebhookEvents.ParseTypes(EventTypes);

    public bool WantsEvent(WebhookEventType type) => SubscribedTypes().Contains(type);
}
