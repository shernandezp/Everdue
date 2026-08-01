using Everdue.Server.Domain;

namespace Everdue.Server.Application.Contracts;

/// <summary>
/// One column of a trend axis. <paramref name="Partial"/> marks the period still in progress — a
/// trend must not render a half-finished week as a collapse.
/// </summary>
public sealed record BucketAxisDto(string Key, string Label, DateOnly Start, bool Partial);

public sealed record BucketPointDto(
    string Key,
    string Label,
    DateOnly Start,
    bool Partial,
    int OnTime,
    int Late,
    int Missed,
    double? Rate);

/// <summary>
/// Compliance for one responsibility over the window.
///
/// <paramref name="Rate"/> is null both when there is nothing to divide and when the denominator is
/// too small to be honest — <paramref name="RateSuppressed"/> tells the two apart, and the raw
/// on-time/concluded pair is always present so a screen never shows a percentage on its own.
///
/// The drill-through opens this responsibility's occurrences in the window, which is
/// <c>Concluded + InFlight</c> rows.
///
/// <paramref name="Active"/> and <paramref name="Paused"/> travel with the row because history is
/// still reported for a retired or paused responsibility — the work did happen — and a manager must be
/// able to tell "nobody is doing this" from "nobody is expected to any more".
/// </summary>
public sealed record ComplianceRowDto(
    Guid ResponsibilityId,
    string Title,
    Guid OwnerUserId,
    string OwnerName,
    Guid? EntityId,
    string? EntityName,
    string? DepartmentName,
    bool Active,
    bool Paused,
    int OnTime,
    int Late,
    int Missed,
    int Concluded,
    int InFlight,
    double? Rate,
    bool RateSuppressed,
    IReadOnlyList<BucketPointDto> Trend,
    DrillThrough DrillThrough);

/// <summary>One occurrence as a chip in the ✅/❌/⏸ strip: the "Week 29 done, Week 30 missed" series.</summary>
public sealed record StripPointDto(
    Guid WorkItemId,
    string Label,
    DateOnly PeriodStart,
    WorkItemStatus Status,
    HoldReason? HoldReason,
    bool PeriodConcluded);

/// <summary>
/// The active and paused flags live on <paramref name="Summary"/> rather than being repeated here: one
/// row shape, read the same way by the table and by this page.
/// </summary>
public sealed record ResponsibilityComplianceDto(
    Guid ResponsibilityId,
    string Title,
    string OwnerName,
    ComplianceRowDto Summary,
    IReadOnlyList<BucketPointDto> Buckets,
    IReadOnlyList<StripPointDto> Strip);

/// <summary>
/// Reliability for one person. Management information for deciding where to help — never a ranking:
/// there is no position column, and volume travels with the rate everywhere it is shown.
///
/// The rate is attributed to the item's **current** owner, and covers occurrences only (a one-off task
/// can never be missed, so counting it would inflate every rate with work that cannot fail).
/// <paramref name="HandedOverInWindow"/> exists so a number produced partly by somebody else is
/// visible rather than implied.
/// </summary>
public sealed record ReliabilityRowDto(
    Guid UserId,
    string DisplayName,
    int OnTime,
    int Late,
    int Missed,
    int Concluded,
    int InFlight,
    double? Rate,
    bool RateSuppressed,
    int ExternallyBlocked,
    double BlockedDays,
    int OneOffCompleted,
    int HandedOverInWindow,
    DrillThrough DrillThrough);

public sealed record ConcentrationPointDto(string BucketKey, int Occurrences, int OneOffs)
{
    public int Total => Occurrences + OneOffs;
}

public sealed record ConcentrationRowDto(
    Guid EntityId,
    string EntityName,
    EntityType EntityType,
    int Total,
    IReadOnlyList<ConcentrationPointDto> Points,
    DrillThrough DrillThrough);

/// <summary>
/// Completed work per entity per bucket — a **count of completed work items**, not effort in hours.
/// The ledger holds no time, so a two-minute call and a full-day inspection both count as one; the
/// screens say "completed work" for that reason.
///
/// <paramref name="UnlinkedTotal"/> is the honest part: work nobody linked to an entity cannot appear
/// in any row, so the amount of it is reported instead of quietly skewing the chart.
/// </summary>
public sealed record ConcentrationSeriesDto(
    IReadOnlyList<BucketAxisDto> Buckets,
    IReadOnlyList<ConcentrationRowDto> Rows,
    int OmittedEntities,
    int UnlinkedTotal);

/// <summary>
/// Hold aging per reason, in **calendar** days.
///
/// <paramref name="CurrentDrillThrough"/> is present only for the holds that are still open right now:
/// a hold that has ended leaves no queryable trace on the work item (releasing, starting or completing
/// clears the reason), so the wait-time figures deliberately carry no link rather than a dead one.
/// </summary>
public sealed record HoldAgingRowDto(
    HoldReason Reason,
    int Holds,
    int Items,
    double TotalWaitDays,
    double AverageWaitDays,
    double LongestWaitDays,
    int StillOnHold,
    DrillThrough? CurrentDrillThrough);

public sealed record HoldAgingEntityRowDto(
    Guid? EntityId,
    string EntityName,
    EntityType? EntityType,
    int Holds,
    int Items,
    double TotalWaitDays,
    double AverageWaitDays,
    double LongestWaitDays,
    int StillOnHold);

public sealed record HoldAgingDto(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<HoldAgingRowDto> ByReason,
    IReadOnlyList<HoldAgingEntityRowDto> ByEntity,
    int OmittedEntities);

/// <summary>
/// A responsibility that keeps being missed. <paramref name="Evaluated"/> is how many of its most
/// recent concluded periods were judged, so "3 of 8" and "3 of 3" are never confused.
/// </summary>
public sealed record ChronicResponsibilityDto(
    Guid ResponsibilityId,
    string Title,
    string OwnerName,
    string? EntityName,
    int Missed,
    int Evaluated,
    DateOnly? LastMissedPeriodStart,
    DrillThrough DrillThrough);
