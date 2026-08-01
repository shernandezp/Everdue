using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.WorkItems;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Reports;

/// <summary>
/// The manager's home screen. Every card counts rows through the same filter object it hands back as
/// its drill-through, so a card can never disagree with the list it opens.
/// </summary>
public sealed class ExceptionsReportHandler(IEverdueDbContext db, ITenantProvider tenants, IClock clock)
    : IRequestHandler<ExceptionsReportQuery, ExceptionsReportDto>
{
    public async Task<ExceptionsReportDto> Handle(ExceptionsReportQuery request, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var timeZone = await tenants.GetTimeZoneAsync(cancellationToken);
        var today = TenantTime.LocalDate(now, timeZone);

        var dayStart = TenantTime.StartOfDay(today, timeZone);
        var dayEndInclusive = TenantTime.StartOfDay(today.AddDays(1), timeZone).AddTicks(-1);

        // Default window for "missed in range": the trailing 30 days up to the end of today.
        var rangeFrom = request.From ?? TenantTime.StartOfDay(today.AddDays(-30), timeZone);
        var rangeTo = request.To ?? dayEndInclusive;

        var filter = request.Filter;

        var dueToday = Base(filter) with { DueFrom = dayStart, DueTo = dayEndInclusive };
        var completedToday = Base(filter, WorkItemStatus.Completed, WorkItemStatus.CompletedLate) with
        {
            CompletedFrom = dayStart,
            CompletedTo = dayEndInclusive,
        };
        // Overdue is derived from the outstanding statuses (see WorkItemQueries.Filter), so starting
        // a task never removes it from this card.
        var overdue = Base(filter) with { Overdue = true };

        // "Missed" on this screen means still missed: an item completed late is no longer something
        // a manager must act on today. Compliance counting (where CompletedLate stays a miss forever)
        // lives in the entity-health report.
        var missed = Base(filter, WorkItemStatus.Missed) with { DueFrom = rangeFrom, DueTo = rangeTo };
        var onHold = Base(filter, WorkItemStatus.OnHold);

        var holdRows = await Apply(onHold, now)
            .Where(w => w.HoldReason != null)
            .GroupBy(w => w.HoldReason!.Value)
            .Select(g => new { Reason = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var holdStarts = await OldestHoldPerReasonAsync(onHold, now, cancellationToken);

        var byReason = holdRows
            .OrderByDescending(r => r.Count)
            .Select(r => new HoldReasonGroupDto(
                r.Reason,
                r.Count,
                holdStarts.TryGetValue(r.Reason, out var heldAt) ? heldAt : null,
                DrillThroughFactory.For(onHold with { HoldReason = r.Reason.ToString() })))
            .ToArray();

        return new ExceptionsReportDto(
            now,
            today,
            await Metric(dueToday, now, cancellationToken),
            await Metric(completedToday, now, cancellationToken),
            await Metric(overdue, now, cancellationToken),
            await Metric(missed, now, cancellationToken),
            await Metric(onHold, now, cancellationToken),
            byReason,
            await ReassignedAsync(rangeFrom, rangeTo, cancellationToken));
    }

    /// <summary>
    /// Counted straight off the event log through the existing (TenantId, EventType, Timestamp) index
    /// — no new index, and no JSON predicate, which neither provider could do portably anyway.
    /// </summary>
    private async Task<ReassignmentSummaryDto> ReassignedAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var count = await db.WorkItemEvents.AsNoTracking()
            .CountAsync(
                e => e.EventType == WorkItemEventType.Reassigned && e.Timestamp >= from && e.Timestamp <= to,
                cancellationToken);

        // The first hand-over ever recorded as one. Before it, owner changes are v1 `Updated` rows,
        // and saying so is better than quietly reporting a number that starts mid-history.
        var countingSince = await db.WorkItemEvents.AsNoTracking()
            .Where(e => e.EventType == WorkItemEventType.Reassigned)
            .OrderBy(e => e.Timestamp)
            .Select(e => (DateTimeOffset?)e.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        return new ReassignmentSummaryDto(count, from, to, countingSince);
    }

    internal static ListWorkItemsQuery Base(ReportFilter filter, params WorkItemStatus[] statuses)
        => ListWorkItemsQuery.For(
            ownerId: filter.OwnerId,
            departmentId: filter.DepartmentId,
            entityType: filter.EntityType,
            statuses: statuses);

    private async Task<MetricDto> Metric(ListWorkItemsQuery query, DateTimeOffset now, CancellationToken cancellationToken)
        => new(await Apply(query, now).CountAsync(cancellationToken), DrillThroughFactory.For(query));

    private IQueryable<WorkItem> Apply(ListWorkItemsQuery query, DateTimeOffset now)
        => WorkItemQueries.Filter(db.WorkItems.AsNoTracking(), query, now);

    /// <summary>
    /// When the current hold started, taken from the event log: the most recent transition into
    /// OnHold for each item. This is the raw material v2's hold-aging analysis is built from.
    /// </summary>
    private async Task<Dictionary<HoldReason, DateTimeOffset>> OldestHoldPerReasonAsync(
        ListWorkItemsQuery onHold,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var items = await Apply(onHold, now)
            .Where(w => w.HoldReason != null)
            .Select(w => new { w.Id, Reason = w.HoldReason!.Value })
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return [];
        }

        var ids = items.Select(i => i.Id).ToArray();

        var starts = await db.WorkItemEvents.AsNoTracking()
            .Where(e => ids.Contains(e.WorkItemId) && e.ToStatus == WorkItemStatus.OnHold)
            .GroupBy(e => e.WorkItemId)
            .Select(g => new { WorkItemId = g.Key, HeldAt = g.Max(e => e.Timestamp) })
            .ToDictionaryAsync(x => x.WorkItemId, x => x.HeldAt, cancellationToken);

        return items
            .Where(i => starts.ContainsKey(i.Id))
            .GroupBy(i => i.Reason)
            .ToDictionary(g => g.Key, g => g.Min(i => starts[i.Id]));
    }
}
