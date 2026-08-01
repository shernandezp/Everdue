using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Reports;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Insights;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// Reliability per person — the one report in this version that measures people, and therefore the one
/// with rules attached:
///
/// <list type="bullet">
/// <item>occurrences only: a one-off task can never be missed, so counting it would inflate every rate
/// with work that cannot fail. One-off completions travel as their own volume column instead.</item>
/// <item>external waits stay in the denominator and are shown beside it. Taking them out would make the
/// denominator something a person could manage by parking work on hold, which is the opposite of what
/// the hold taxonomy is for.</item>
/// <item>attribution is to the item's current owner, stated on the screen, with the number of
/// hand-overs in the window beside it.</item>
/// </list>
///
/// There is no rank, no position and no target here, by design.
/// </summary>
public sealed class ReliabilityHandler(
    IEverdueDbContext db,
    ITenantProvider tenants,
    IUserDirectory users,
    IClock clock,
    IOptions<InsightsOptions> options)
    : IRequestHandler<ReliabilityQuery, IReadOnlyList<ReliabilityRowDto>>
{
    public async Task<IReadOnlyList<ReliabilityRowDto>> Handle(
        ReliabilityQuery request,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var settings = options.Value;
        var timeZone = await tenants.GetTimeZoneAsync(cancellationToken);

        var filter = request.Scope();
        var window = request.Window(BucketKind.Week, timeZone, now, settings);

        var occurrences = await new OccurrenceLedgerReader(db).InWindowAsync(filter, window, cancellationToken);
        var waits = await ExternalWaitsAsync(window, now, cancellationToken);
        var oneOffs = await OneOffCompletionsAsync(filter, window, cancellationToken);
        var handOvers = await HandOversAsync(filter, window, cancellationToken);

        var owners = occurrences.Select(o => o.OwnerUserId).Concat(oneOffs.Keys).Distinct().ToArray();
        var directory = await users.MapAsync(owners, cancellationToken);

        var byOwner = occurrences.GroupBy(o => o.OwnerUserId).ToDictionary(group => group.Key, group => group.ToArray());

        var rows = owners
            .Select(ownerId => Row(
                ownerId,
                byOwner.GetValueOrDefault(ownerId, []),
                waits,
                oneOffs.GetValueOrDefault(ownerId),
                handOvers.GetValueOrDefault(ownerId),
                directory,
                filter,
                window,
                now,
                settings))
            .ToArray();

        return Sort(rows, request.ResolvedSort, request.ResolvedDescending).ToArray();
    }

    private static ReliabilityRowDto Row(
        Guid ownerId,
        IReadOnlyList<LedgerOccurrence> owned,
        IReadOnlyDictionary<Guid, List<HoldInterval>> waits,
        int oneOffCompleted,
        int handedOver,
        IReadOnlyDictionary<Guid, UserSummary> directory,
        InsightsFilter filter,
        InsightsWindow window,
        DateTimeOffset now,
        InsightsOptions settings)
    {
        var tally = new ComplianceTally();
        var blocked = 0;
        var blockedDays = 0d;

        foreach (var occurrence in owned)
        {
            tally.Add(occurrence.Status, occurrence.IsConcludedAt(now));

            if (!waits.TryGetValue(occurrence.Id, out var intervals))
            {
                continue;
            }

            // "Blocked" means an external wait overlapped the period the occurrence had to be done in —
            // not merely that the item was on hold at some point in its life.
            if (intervals.Any(interval => interval.Overlaps(occurrence.PeriodStart, occurrence.PeriodEnd)))
            {
                blocked++;
            }

            blockedDays += intervals
                .Select(interval => interval.Clip(window.From, window.EffectiveTo(now)))
                .OfType<HoldInterval>()
                .Sum(interval => interval.Days);
        }

        return new ReliabilityRowDto(
            ownerId,
            InsightsRows.DisplayName(directory, ownerId),
            tally.OnTime,
            tally.Late,
            tally.Missed,
            tally.Concluded,
            tally.InFlight,
            tally.Rate(settings.MinOccurrencesForRate),
            tally.IsSuppressed(settings.MinOccurrencesForRate),
            blocked,
            Math.Round(blockedDays, 1, MidpointRounding.AwayFromZero),
            oneOffCompleted,
            handedOver,

            // Their occurrences in the window: Concluded + InFlight rows, and no one-off work, which is
            // why the work-item filter grew an occurrences flag alongside this report.
            DrillThroughFactory.For(InsightsRows.Occurrences(filter, window) with { OwnerId = ownerId }));
    }

    /// <summary>
    /// External holds (waiting on a customer or a supplier), indexed by work item. Internal waits — an
    /// approval, missing information — are ours to chase and are not an excuse for a miss.
    /// </summary>
    private async Task<Dictionary<Guid, List<HoldInterval>>> ExternalWaitsAsync(
        InsightsWindow window,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var intervals = await new HoldIntervalReader(db).ReadAsync(window.To, now, cancellationToken);

        return intervals
            .Where(interval => interval.Reason.IsExternalWait())
            .GroupBy(interval => interval.WorkItemId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private async Task<Dictionary<Guid, int>> OneOffCompletionsAsync(
        InsightsFilter filter,
        InsightsWindow window,
        CancellationToken cancellationToken)
    {
        var rows = await InsightsScope.Apply(
                db.WorkItems.AsNoTracking()
                    .Where(w => w.ResponsibilityId == null
                                && (w.Status == WorkItemStatus.Completed || w.Status == WorkItemStatus.CompletedLate)
                                && w.CompletedAt >= window.From
                                && w.CompletedAt < window.To),
                filter)
            .GroupBy(w => w.OwnerUserId)
            .Select(group => new { OwnerId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.OwnerId, row => row.Count);
    }

    /// <summary>
    /// How much of this person's work changed hands inside the window, counted off the event log through
    /// the existing <c>(TenantId, EventType, Timestamp)</c> index and joined to the item's current owner —
    /// the same owner the rate is attributed to, so the two numbers describe the same person.
    ///
    /// Scoped like every other number on the row: filtering the report to one department must not leave
    /// one column counting the whole tenant.
    /// </summary>
    private async Task<Dictionary<Guid, int>> HandOversAsync(
        InsightsFilter filter,
        InsightsWindow window,
        CancellationToken cancellationToken)
    {
        var scoped = InsightsScope.Apply(
            db.WorkItems.AsNoTracking().Where(w => w.Status != WorkItemStatus.Cancelled),
            filter);

        var rows = await db.WorkItemEvents.AsNoTracking()
            .Where(e => e.EventType == WorkItemEventType.Reassigned
                        && e.Timestamp >= window.From
                        && e.Timestamp < window.To)
            .Join(scoped, e => e.WorkItemId, w => w.Id, (_, w) => w.OwnerUserId)
            .GroupBy(ownerId => ownerId)
            .Select(group => new { OwnerId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.OwnerId, row => row.Count);
    }

    private static IEnumerable<ReliabilityRowDto> Sort(
        IEnumerable<ReliabilityRowDto> rows,
        ReliabilitySort sort,
        bool descending)
    {
        if (sort == ReliabilitySort.Rate)
        {
            var ordered = rows.OrderBy(r => r.Rate is null);

            return (descending ? ordered.ThenByDescending(r => r.Rate) : ordered.ThenBy(r => r.Rate))
                .ThenBy(r => r.DisplayName);
        }

        if (sort == ReliabilitySort.Name)
        {
            return descending ? rows.OrderByDescending(r => r.DisplayName) : rows.OrderBy(r => r.DisplayName);
        }

        Func<ReliabilityRowDto, double> key = sort switch
        {
            ReliabilitySort.OnTime => row => row.OnTime,
            ReliabilitySort.Late => row => row.Late,
            ReliabilitySort.Concluded => row => row.Concluded,
            ReliabilitySort.ExternallyBlocked => row => row.ExternallyBlocked,
            ReliabilitySort.BlockedDays => row => row.BlockedDays,
            _ => row => row.Missed,
        };

        return (descending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(r => r.DisplayName);
    }
}
