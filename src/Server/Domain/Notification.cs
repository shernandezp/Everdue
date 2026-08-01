namespace Everdue.Server.Domain;

/// <summary>
/// One thing one person should know about. The row is both the in-app notification and the head of
/// the outbox — which is why an install with no channels configured still works completely: the
/// notification exists and is readable whether or not anything ever leaves the machine.
/// </summary>
public class Notification : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    /// <summary>Who is being told. Never a group — fan-out happens at enqueue time.</summary>
    public Guid UserId { get; set; }

    public NotificationType Type { get; set; }

    public Guid? WorkItemId { get; set; }
    public Guid? CommentId { get; set; }

    /// <summary>
    /// Render parameters only (title, entity, due date, actor) — never rendered text. The bell
    /// renders these in the reader's UI language; channels render them in the recipient's stored
    /// language. One set of facts, two renderers.
    /// </summary>
    public string? DataJson { get; set; }

    /// <summary>
    /// Set only for things that must happen once (a due-today reminder, a miss). Null for repeatable
    /// events, and NULLs are distinct in a unique index on both providers — which is exactly the
    /// wanted behaviour and is what makes the reminder run restart-safe with no "last run" marker.
    /// </summary>
    public string? DedupeKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    public WorkItem? WorkItem { get; set; }
}
