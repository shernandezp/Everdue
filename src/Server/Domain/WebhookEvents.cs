namespace Everdue.Server.Domain;

/// <summary>
/// The six events a subscriber can ask for. Appended to, never reordered — the names travel on the
/// wire and are stored in <see cref="WebhookSubscription.EventTypes"/>.
///
/// <c>Completed</c> covers a late completion, flagged <c>late</c> in the payload: splitting them would
/// force anybody who wants completions to subscribe twice. <c>Rescheduled</c> is deliberately absent —
/// nothing asked for it, and an unused event type is a payload shape we would have to keep forever.
/// Adding one later is additive and therefore free.
/// </summary>
public enum WebhookEventType
{
    WorkItemCreated = 0,
    WorkItemCompleted = 1,
    WorkItemMissed = 2,
    WorkItemOnHold = 3,
    WorkItemReassigned = 4,
    EntityCreated = 5,

    /// <summary>Sent only by the admin "test" button, so an endpoint can be proved before it is trusted.</summary>
    Ping = 6,
}

/// <summary>
/// Turns the <see cref="WorkItemEvent"/> the mutator has already written into a webhook event type —
/// which is why there is no second event system, no projector and no cursor. The occurrence engine's
/// statelessness is deliberate, and a "last processed event" marker would undo it.
/// </summary>
public static class WebhookEvents
{
    /// <summary>The wire name, as it appears in the payload's <c>type</c> and in a subscription's list.</summary>
    public static string WireName(WebhookEventType type) => type switch
    {
        WebhookEventType.WorkItemCreated => "workitem.created",
        WebhookEventType.WorkItemCompleted => "workitem.completed",
        WebhookEventType.WorkItemMissed => "workitem.missed",
        WebhookEventType.WorkItemOnHold => "workitem.onhold",
        WebhookEventType.WorkItemReassigned => "workitem.reassigned",
        WebhookEventType.EntityCreated => "entity.created",
        WebhookEventType.Ping => "ping",
        _ => type.ToString(),
    };

    /// <summary>
    /// Which webhook event, if any, a written work-item event represents. Null for everything a
    /// subscriber has no business hearing about — a comment, a plain field edit, a reschedule.
    /// </summary>
    public static WebhookEventType? From(WorkItemEventType eventType, WorkItemStatus? toStatus) => eventType switch
    {
        WorkItemEventType.Created => WebhookEventType.WorkItemCreated,
        WorkItemEventType.Reassigned => WebhookEventType.WorkItemReassigned,

        WorkItemEventType.StatusChanged => toStatus switch
        {
            WorkItemStatus.Completed or WorkItemStatus.CompletedLate => WebhookEventType.WorkItemCompleted,
            WorkItemStatus.Missed => WebhookEventType.WorkItemMissed,
            WorkItemStatus.OnHold => WebhookEventType.WorkItemOnHold,
            _ => null,
        },

        _ => null,
    };

    /// <summary>
    /// Parses a stored or submitted list. Unknown names are dropped rather than throwing: a
    /// subscription written by a newer version must not make an older one unable to read its own table.
    /// </summary>
    public static IReadOnlyList<WebhookEventType> ParseTypes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Enum.TryParse<WebhookEventType>(part, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : (WebhookEventType?)null)
            .Where(parsed => parsed is not null)
            .Select(parsed => parsed!.Value)
            .Distinct()
            .ToArray();
    }

    public static string FormatTypes(IEnumerable<WebhookEventType> types)
        => string.Join(',', types.Distinct().Select(t => t.ToString()));

    /// <summary>
    /// What a subscription may ask for. <see cref="WebhookEventType.Ping"/> is excluded: it is sent by
    /// the test button, not subscribed to.
    /// </summary>
    public static readonly WebhookEventType[] Subscribable =
    [
        WebhookEventType.WorkItemCreated,
        WebhookEventType.WorkItemCompleted,
        WebhookEventType.WorkItemMissed,
        WebhookEventType.WorkItemOnHold,
        WebhookEventType.WorkItemReassigned,
        WebhookEventType.EntityCreated,
    ];
}
