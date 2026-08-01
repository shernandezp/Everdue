namespace Everdue.Server.Domain;

/// <summary>Application role. Stored as an int on the user row (see Infrastructure/Identity/AppUser).</summary>
public enum UserRole
{
    Member = 0,
    Admin = 1,
}

/// <summary>
/// The thin reference taxonomy. Entities are references, never business data (guardrails §2).
/// </summary>
public enum EntityType
{
    Customer = 0,
    Supplier = 1,
    Equipment = 2,
    Department = 3,
    Company = 4,
}

/// <summary>
/// What a custom field on an entity can hold. Four scalar types and no more: anything richer starts
/// answering questions about the customer rather than about the work (guardrails §2).
/// </summary>
public enum EntityFieldType
{
    Text = 0,
    Number = 1,
    Date = 2,
    Select = 3,
}

/// <summary>
/// What an API key may do. The scope splits reading from writing; <em>which endpoints</em> a key may
/// reach at all is a separate, stricter question answered by the endpoint allow-list.
/// </summary>
public enum ApiKeyScope
{
    ReadOnly = 0,
    ReadWrite = 1,
}

public enum RecurrenceKind
{
    Daily = 0,
    WeeklyOnDays = 1,
    MonthlyOnDay = 2,
    Yearly = 3,
}

/// <summary>
/// Occurrence / task state. <c>Overdue</c> is deliberately absent: it is derived
/// (<c>Status ∈ {Open, OnHold} &amp;&amp; now &gt; DueDate</c>), never stored.
/// </summary>
public enum WorkItemStatus
{
    Open = 0,
    Completed = 1,
    CompletedLate = 2,
    Missed = 3,
    OnHold = 4,
    Cancelled = 5,

    /// <summary>
    /// Someone has picked this up. Appended (not inserted) so stored values stay stable.
    ///
    /// It is the one status that changes no report: every count that treats Open as actionable
    /// treats this identically. It exists so a manager can split the actionable pile into "being
    /// done" and "still queued" — and for no other reason. It never protects an item from a miss.
    /// </summary>
    InProgress = 6,
}

/// <summary>Fixed, tiny taxonomy — small enough that staff actually record it.</summary>
public enum HoldReason
{
    WaitingCustomer = 0,
    WaitingSupplier = 1,
    WaitingApproval = 2,
    MissingInformation = 3,
    Other = 4,
}

public static class HoldReasonExtensions
{
    /// <summary>
    /// Waiting on somebody outside the team. Stated once here because the reliability report shows
    /// these waits beside a person's miss count — the difference between "unreliable" and "was waiting
    /// on the customer" is the whole reason that report is safe to look at. An approval and a piece of
    /// missing information are ours to chase, so neither counts as external.
    /// </summary>
    public static bool IsExternalWait(this HoldReason reason)
        => reason is HoldReason.WaitingCustomer or HoldReason.WaitingSupplier;
}

public enum WorkItemEventType
{
    Created = 0,
    StatusChanged = 1,
    Rescheduled = 2,
    CommentAdded = 3,

    /// <summary>
    /// A descriptive field changed (title, description, owner, entity, department). Anyone may edit
    /// anyone's work — a small team covers for each other — so the record of who changed what is
    /// what keeps that safe.
    /// </summary>
    Updated = 4,

    /// <summary>
    /// The same field-diff payload as <see cref="Updated"/>, but typed separately when the diff
    /// contains the owner. Appended in v1.5 so "who was handed what, and when" is an indexed query
    /// on <c>(TenantId, EventType, Timestamp)</c> instead of a scan through JSON — which neither
    /// provider can do portably. v1 rows stay <see cref="Updated"/>; nothing is rewritten.
    /// </summary>
    Reassigned = 5,
}

/// <summary>Who is performing a status transition. The engine may do things users may not, and vice versa.</summary>
public enum TransitionActor
{
    User = 0,
    Engine = 1,
}

public static class WorkItemStatusExtensions
{
    /// <summary>Counted as "the work happened" by activity reports (last-activity, timelines).</summary>
    public static bool IsCompletion(this WorkItemStatus status)
        => status is WorkItemStatus.Completed or WorkItemStatus.CompletedLate;

    /// <summary>Counted as a miss by compliance reports. CompletedLate never erases the miss.</summary>
    public static bool CountsAsMissed(this WorkItemStatus status)
        => status is WorkItemStatus.Missed or WorkItemStatus.CompletedLate;

    /// <summary>Still workable — the states the board shows as actionable.</summary>
    public static bool IsWorkable(this WorkItemStatus status)
        => status is WorkItemStatus.Open or WorkItemStatus.InProgress or WorkItemStatus.OnHold or WorkItemStatus.Missed;

    /// <summary>
    /// Outstanding work: not finished, not cancelled, not yet missed. Every report that asks
    /// "is this still on someone's plate" asks through here, so adding a state cannot silently
    /// drop rows out of a manager's numbers.
    /// </summary>
    public static bool IsOutstanding(this WorkItemStatus status)
        => status is WorkItemStatus.Open or WorkItemStatus.InProgress or WorkItemStatus.OnHold;
}
