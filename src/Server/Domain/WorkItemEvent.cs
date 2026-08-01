namespace Everdue.Server.Domain;

/// <summary>
/// Append-only history, written on every mutation. Little reads it yet, but hold-aging analysis and
/// any future audit log are built entirely from these rows, which is why it's written now.
/// </summary>
public class WorkItemEvent : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    public Guid WorkItemId { get; set; }

    /// <summary>Null = written by the occurrence engine.</summary>
    public Guid? UserId { get; set; }

    public DateTimeOffset Timestamp { get; set; }
    public WorkItemEventType EventType { get; set; }

    public WorkItemStatus? FromStatus { get; set; }
    public WorkItemStatus? ToStatus { get; set; }

    /// <summary>Free-form JSON payload (hold reason, reschedule from/to, prior status at miss, …).</summary>
    public string? DataJson { get; set; }

    public WorkItem? WorkItem { get; set; }
}
