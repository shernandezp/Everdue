using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Reports;
using Everdue.Server.Application.WorkItems;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// Row assembly shared by the insight handlers, and the one place a drill-through is built.
///
/// Every number is handed back with the exact <see cref="ListWorkItemsQuery"/> that produces its rows,
/// which is what makes "the list behind a number totals that number" true by construction rather than
/// by discipline — the v1 invariant, extended to the new numbers.
/// </summary>
internal static class InsightsRows
{
    public static ComplianceRowDto Compliance(
        ResponsibilityLabel label,
        ComplianceCalculator.Result result,
        IReadOnlyDictionary<Guid, UserSummary> users,
        InsightsFilter filter,
        InsightsWindow window,
        DateTimeOffset now,
        int minimumForRate)
    {
        var tally = result.Tally;

        return new ComplianceRowDto(
            label.Id,
            label.Title,
            label.OwnerUserId,
            DisplayName(users, label.OwnerUserId),
            label.EntityId,
            label.EntityName,
            label.DepartmentName,
            label.Active,
            label.PausedUntil is { } until && until > now,
            tally.OnTime,
            tally.Late,
            tally.Missed,
            tally.Concluded,
            tally.InFlight,
            tally.Rate(minimumForRate),
            tally.IsSuppressed(minimumForRate),
            result.Trend,

            // Opens this responsibility's occurrences in the same window: Concluded + InFlight rows.
            // Expressible exactly because the window's boundaries are local midnights, and an
            // occurrence's period start (local 00:00) and due date (local 23:59:59) always fall on the
            // same side of one.
            DrillThroughFactory.For(Occurrences(filter, window) with { ResponsibilityId = label.Id }));
    }

    public static string DisplayName(IReadOnlyDictionary<Guid, UserSummary> users, Guid id)
        => users.TryGetValue(id, out var user) ? user.DisplayName : "—";

    /// <summary>The scope filters plus the window, as the one work-item query the numbers were counted with.</summary>
    public static ListWorkItemsQuery Occurrences(InsightsFilter filter, InsightsWindow window)
        => OccurrencesBetween(filter, window.From, window.To.AddTicks(-1));

    /// <summary>
    /// The same query over an explicit instant range, for the numbers whose span is not the reporting
    /// window — chronic detection judges a responsibility's own last N periods.
    /// </summary>
    public static ListWorkItemsQuery OccurrencesBetween(InsightsFilter filter, DateTimeOffset from, DateTimeOffset to)
        => ListWorkItemsQuery.For(
            ownerId: filter.OwnerId,
            departmentId: filter.DepartmentId,
            entityId: filter.EntityId,
            entityType: filter.EntityType,
            occurrences: true) with
        {
            DueFrom = from,
            DueTo = to,
        };
}
