using Everdue.Server.Domain;

namespace Everdue.Server.Application.Contracts;

/// <summary>
/// Every aggregate number carries the exact <c>/api/v1/workitems</c> query that produces its rows.
/// The UI links; it never re-derives a filter and never disagrees with the number above it.
/// </summary>
public sealed record DrillThrough(IReadOnlyDictionary<string, string> WorkItemQuery)
{
    public static DrillThrough From(params (string Key, string? Value)[] parts)
        => new(parts
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .ToDictionary(p => p.Key, p => p.Value!));
}

public sealed record MetricDto(int Count, DrillThrough DrillThrough);

public sealed record HoldReasonGroupDto(HoldReason Reason, int Count, DateTimeOffset? OldestHoldAt, DrillThrough DrillThrough);

public sealed record ExceptionsReportDto(
    DateTimeOffset GeneratedAt,
    DateOnly LocalDate,
    MetricDto DueToday,
    MetricDto CompletedToday,
    MetricDto Overdue,
    MetricDto MissedInRange,
    MetricDto OnHold,
    IReadOnlyList<HoldReasonGroupDto> OnHoldByReason,
    ReassignmentSummaryDto Reassigned);

/// <summary>
/// How much work changed hands in the period. Visibility inside an existing screen, not a new report.
///
/// <paramref name="CountingSince"/> is the honest part: owner changes used to be recorded as ordinary edits,
/// so counting starts when reassignment tracking was added and the screen says so rather than implying the number
/// covers all of history.
/// </summary>
public sealed record ReassignmentSummaryDto(int Count, DateTimeOffset From, DateTimeOffset To, DateTimeOffset? CountingSince);

public sealed record EntityHealthRowDto(
    Guid EntityId,
    string EntityName,
    EntityType EntityType,
    int Open,
    int Overdue,
    int Missed30,
    int Missed60,
    int Missed90,
    int OnHold,
    DateTimeOffset? LastActivityAt,
    int? DaysSinceLastActivity,
    DrillThrough DrillThrough);

public sealed record NeglectRowDto(
    Guid EntityId,
    string EntityName,
    EntityType EntityType,
    DateTimeOffset? LastActivityAt,
    int? DaysSinceLastActivity,
    int OpenCount,
    DrillThrough DrillThrough);

public sealed record BlockedByEntityGroupDto(
    Guid? EntityId,
    string EntityName,
    EntityType? EntityType,
    int Total,
    DateTimeOffset? OldestHoldAt,
    IReadOnlyList<HoldReasonGroupDto> Reasons,
    DrillThrough DrillThrough);

public sealed record TimelineItemDto(
    Guid WorkItemId,
    Guid? ResponsibilityId,
    string? ResponsibilityTitle,
    string Title,
    DateTimeOffset SortDate,
    DateTimeOffset? PeriodStart,
    DateTimeOffset DueDate,
    WorkItemStatus Status,
    HoldReason? HoldReason,
    string? HoldReasonText,
    DateTimeOffset? CompletedAt,
    bool IsOccurrence,

    /// <summary>
    /// Checklist progress, or null when the row has no checklist — the same shape the work-item DTO uses, so the
    /// badge component is shared rather than reimplemented for the timeline.
    /// </summary>
    int? ChecklistTotal = null,
    int? ChecklistChecked = null);

public sealed record EntityTimelineDto(
    Guid EntityId,
    string EntityName,
    EntityType EntityType,
    DateTimeOffset? LastActivityAt,
    IReadOnlyList<TimelineItemDto> Items);
