namespace Everdue.Server.Domain;

/// <summary>
/// One table for occurrences AND one-off tasks. <see cref="ResponsibilityId"/> null means one-off.
/// Occurrence rows are the ledger: they persist forever and a miss is never erased.
/// </summary>
public class WorkItem : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    /// <summary>Null = one-off task. Non-null = engine-generated occurrence.</summary>
    public Guid? ResponsibilityId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid OwnerUserId { get; set; }
    public Guid? EntityId { get; set; }
    public Guid? DepartmentId { get; set; }

    /// <summary>Occurrences: scheduled date 23:59:59 local. One-offs: whatever the user picked.</summary>
    public DateTimeOffset DueDate { get; set; }

    /// <summary>Occurrences only: scheduled date 00:00 local.</summary>
    public DateTimeOffset? PeriodStart { get; set; }

    /// <summary>Occurrences only: the NEXT scheduled date 00:00 local. At this instant the item is missed.</summary>
    public DateTimeOffset? PeriodEnd { get; set; }

    public WorkItemStatus Status { get; set; } = WorkItemStatus.Open;

    public HoldReason? HoldReason { get; set; }
    public string? HoldReasonText { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }

    public Responsibility? Responsibility { get; set; }
    public Entity? Entity { get; set; }
    public Department? Department { get; set; }

    public bool IsOccurrence => ResponsibilityId is not null;

    /// <summary>Derived, never stored. Anything still outstanding can be overdue — including work in progress.</summary>
    public bool IsOverdueAt(DateTimeOffset now) => Status.IsOutstanding() && now > DueDate;

    /// <summary>
    /// Drops the hold. Both fields together, always: a reason left behind without a hold is what makes
    /// the hold-aging report count a wait that ended. Starting, completing and reopening all release a
    /// hold, and each of them used to clear the pair by hand.
    /// </summary>
    public void ClearHold()
    {
        HoldReason = null;
        HoldReasonText = null;
    }
}
