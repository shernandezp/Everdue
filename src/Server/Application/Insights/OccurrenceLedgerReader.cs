using System.Linq.Expressions;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// One occurrence, narrowed to the columns every rate is computed from. Never a tracked
/// <see cref="WorkItem"/>: the insight surface reads the ledger, it never writes to it.
/// </summary>
public sealed record LedgerOccurrence(
    Guid Id,
    Guid ResponsibilityId,
    Guid OwnerUserId,
    Guid? EntityId,
    Guid? DepartmentId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    WorkItemStatus Status,
    HoldReason? HoldReason,
    DateTimeOffset? CompletedAt)
{
    /// <summary>The period has been judged. See <see cref="Domain.Insights.ComplianceTally"/> for why this is not a status.</summary>
    public bool IsConcludedAt(DateTimeOffset now) => PeriodEnd <= now;
}

/// <summary>
/// The single occurrence projection compliance, reliability and chronic detection all read. SQL
/// narrows the rows; bucketing and rate maths happen in application code, because a tenant-local ISO
/// week is not portably derivable from a UTC timestamp on both providers.
/// </summary>
internal sealed class OccurrenceLedgerReader(IEverdueDbContext db)
{
    private static readonly Expression<Func<WorkItem, LedgerOccurrence>> Projection = w => new LedgerOccurrence(
        w.Id,
        w.ResponsibilityId!.Value,
        w.OwnerUserId,
        w.EntityId,
        w.DepartmentId,
        w.PeriodStart!.Value,
        w.PeriodEnd!.Value,
        w.Status,
        w.HoldReason,
        w.CompletedAt);

    /// <summary>
    /// Occurrences whose period **starts** inside the window. Both window boundaries are local
    /// midnights, which is what makes this predicate select exactly the rows the drill-through's
    /// <c>DueDate</c> filter selects.
    /// </summary>
    public Task<List<LedgerOccurrence>> InWindowAsync(
        InsightsFilter filter,
        InsightsWindow window,
        CancellationToken cancellationToken)
        => Scoped(filter)
            .Where(w => w.PeriodStart >= window.From && w.PeriodStart < window.To)
            .Select(Projection)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Every concluded occurrence of an active responsibility, with no date bound at all.
    ///
    /// Chronic detection asks for the last N periods of each responsibility, and for a yearly
    /// obligation those span N years — any lookback window would silently exempt exactly the
    /// responsibilities with the longest periods. The projection is four columns wide and the scan is
    /// bounded by the ledger itself; when a tenant's history makes that too slow, the answer is a
    /// future summary table, not a bound that changes the meaning of the rule.
    /// </summary>
    public Task<List<LedgerOccurrence>> ConcludedAsync(
        InsightsFilter filter,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => Scoped(filter)
            .Where(w => w.PeriodEnd <= now && w.Responsibility!.Active)
            .Select(Projection)
            .ToListAsync(cancellationToken);

    private IQueryable<WorkItem> Scoped(InsightsFilter filter)
        => InsightsScope.Apply(
            db.WorkItems.AsNoTracking()
                .Where(w => w.ResponsibilityId != null && w.PeriodStart != null && w.PeriodEnd != null)

                // Occurrences are never cancelled (the transition is one-off only); excluded defensively
                // so a row that somehow is cannot enter a denominator.
                .Where(w => w.Status != WorkItemStatus.Cancelled),
            filter);
}
