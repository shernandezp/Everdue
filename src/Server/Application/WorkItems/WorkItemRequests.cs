using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.WorkItems;

public enum WorkItemView
{
    List = 0,

    /// <summary>Open + on hold + missed-uncompleted + done in the last 7 days. The daily working set.</summary>
    Board = 1,
}

/// <summary>
/// Sortable columns of the work list. Owner is absent on purpose: display names live in the user
/// directory, not on the row, so the database cannot order by them.
/// </summary>
public enum WorkItemSort
{
    DueDate = 0,
    Title = 1,
    Status = 2,
    Entity = 3,
}


/// <summary>
/// The one filter vocabulary. Report drill-throughs are expressed in exactly these parameters, so a
/// dashboard number and the list behind it can never disagree.
///
/// Enum-valued filters are strings on the wire and parsed case-insensitively (see
/// <see cref="EnumQuery"/>); <c>Status</c> additionally accepts a comma-separated list, e.g.
/// "Missed,CompletedLate".
/// </summary>
public sealed record ListWorkItemsQuery(
    string? View = null,
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? EntityId = null,
    Guid? ResponsibilityId = null,
    string? EntityType = null,

    /// <summary>
    /// True = engine-generated occurrences only, false = one-off tasks only. Added with the insight
    /// reports, whose numbers are occurrence-only or one-off-only by definition: without it a rate
    /// over 40 occurrences would drill through to a list that also contains one-off work, and the
    /// number and its rows would disagree.
    /// </summary>
    bool? Occurrences = null,
    string? Status = null,
    string? HoldReason = null,
    DateTimeOffset? DueFrom = null,
    DateTimeOffset? DueTo = null,
    DateTimeOffset? CompletedFrom = null,
    DateTimeOffset? CompletedTo = null,
    bool? Overdue = null,
    bool IncludeCancelled = false,
    string? Search = null,
    string? Sort = null,
    bool Descending = false,
    int? Page = null,
    int? PageSize = null) : IQuery<PagedResult<WorkItemDto>>
{
    public WorkItemView ResolvedView => EnumQuery.ParseOr(View, nameof(View), WorkItemView.List);

    public WorkItemSort ResolvedSort => EnumQuery.ParseOr(Sort, nameof(Sort), WorkItemSort.DueDate);

    public EntityType? ResolvedEntityType => EnumQuery.Parse<EntityType>(EntityType, nameof(EntityType));

    public HoldReason? ResolvedHoldReason => EnumQuery.Parse<HoldReason>(HoldReason, nameof(HoldReason));

    public WorkItemStatus[] ResolvedStatuses => EnumQuery.ParseMany<WorkItemStatus>(Status, nameof(Status));

    /// <summary>Typed construction for report drill-throughs, which build this server-side.</summary>
    public static ListWorkItemsQuery For(
        Guid? ownerId = null,
        Guid? departmentId = null,
        Guid? entityId = null,
        Guid? responsibilityId = null,
        EntityType? entityType = null,
        bool? occurrences = null,
        WorkItemStatus[]? statuses = null,
        HoldReason? holdReason = null)
        => new(
            OwnerId: ownerId,
            DepartmentId: departmentId,
            EntityId: entityId,
            ResponsibilityId: responsibilityId,
            EntityType: entityType?.ToString(),
            Occurrences: occurrences,
            Status: statuses is { Length: > 0 } ? string.Join(',', statuses) : null,
            HoldReason: holdReason?.ToString());
}

public sealed record GetWorkItemQuery(Guid Id) : IQuery<WorkItemDetailDto>;

/// <summary>One-off tasks only. Occurrences are created by the engine and by nothing else.</summary>
public sealed record CreateWorkItemCommand(
    [property: Required, MaxLength(300)] string Title,
    [property: MaxLength(4000)] string? Description,
    Guid OwnerUserId,
    Guid? EntityId,
    Guid? DepartmentId,
    DateTimeOffset DueDate) : ICommand<WorkItemDto>;

/// <summary>Descriptive fields only — status is never a PATCH, and dates move via /reschedule.</summary>
public sealed record UpdateWorkItemCommand(
    Guid Id,
    [property: Required, MaxLength(300)] string Title,
    [property: MaxLength(4000)] string? Description,
    Guid OwnerUserId,
    Guid? EntityId,
    Guid? DepartmentId) : ICommand<WorkItemDto>;

/// <summary>Picks the item up. Purely a coordination signal — it never affects whether a period is missed.</summary>
public sealed record StartWorkItemCommand(Guid Id) : ICommand<WorkItemDto>;

public sealed record CompleteWorkItemCommand(Guid Id) : ICommand<WorkItemDto>;

/// <summary>
/// The reason is nullable so that omitting it fails validation instead of silently defaulting to the
/// first enum member. A hold with no reason is the one thing this taxonomy exists to prevent.
/// </summary>
public sealed record HoldWorkItemCommand(
    Guid Id,
    [property: Required] HoldReason? Reason,
    [property: MaxLength(1000)] string? Text) : ICommand<WorkItemDto>;

public sealed record ReopenWorkItemCommand(Guid Id) : ICommand<WorkItemDto>;

public sealed record CancelWorkItemCommand(Guid Id) : ICommand<WorkItemDto>;

public sealed record RescheduleWorkItemCommand(
    Guid Id,
    DateTimeOffset NewDueDate,
    [property: MaxLength(1000)] string? Note) : ICommand<WorkItemDto>;
