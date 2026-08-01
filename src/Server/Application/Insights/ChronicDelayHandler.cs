using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Reports;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Insights;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// Responsibilities that keep being missed: K misses inside their own last N concluded periods.
///
/// Two integers from configuration, not a rules engine — and window-independent on purpose, because
/// "the last eight periods" of a yearly obligation is eight years, and any date bound would quietly
/// exempt exactly the responsibilities with the longest periods.
/// </summary>
public sealed class ChronicDelayHandler(
    IEverdueDbContext db,
    ITenantProvider tenants,
    IUserDirectory users,
    IClock clock,
    IOptions<InsightsOptions> options)
    : IRequestHandler<ChronicDelayQuery, IReadOnlyList<ChronicResponsibilityDto>>
{
    private sealed record Candidate(Guid ResponsibilityId, int Missed, int Evaluated, DateOnly? LastMissed, DrillThrough DrillThrough);

    public async Task<IReadOnlyList<ChronicResponsibilityDto>> Handle(
        ChronicDelayQuery request,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var settings = options.Value;
        var timeZone = await tenants.GetTimeZoneAsync(cancellationToken);
        var limit = Math.Clamp(request.Limit ?? 5, 1, 50);
        var filter = request.Scope();

        var concluded = await new OccurrenceLedgerReader(db).ConcludedAsync(filter, now, cancellationToken);

        var candidates = concluded
            .GroupBy(o => o.ResponsibilityId)
            .Select(group => Judge(group.Key, group, filter, timeZone, settings))
            .Where(candidate => candidate.Missed >= settings.ChronicMissCount)
            .OrderByDescending(candidate => candidate.Missed)
            .ThenByDescending(candidate => candidate.LastMissed)
            .Take(limit)
            .ToArray();

        var labels = await new ResponsibilityLabelReader(db).ForAsync(
            candidates.Select(candidate => candidate.ResponsibilityId).ToArray(),
            cancellationToken);

        var directory = await users.MapAsync(labels.Values.Select(label => label.OwnerUserId), cancellationToken);

        return candidates
            .Where(candidate => labels.ContainsKey(candidate.ResponsibilityId))
            .Select(candidate =>
            {
                var label = labels[candidate.ResponsibilityId];

                return new ChronicResponsibilityDto(
                    label.Id,
                    label.Title,
                    InsightsRows.DisplayName(directory, label.OwnerUserId),
                    label.EntityName,
                    candidate.Missed,
                    candidate.Evaluated,
                    candidate.LastMissed,
                    candidate.DrillThrough);
            })
            .ToArray();
    }

    /// <summary>
    /// Judges the most recent <c>ChronicWindowPeriods</c> concluded periods, or all of them when there
    /// are fewer: three misses out of the only three periods that exist is chronic by any reading, and
    /// <c>Evaluated</c> travels with the number so "3 of 8" is never confused with "3 of 3".
    /// </summary>
    private static Candidate Judge(
        Guid responsibilityId,
        IEnumerable<LedgerOccurrence> occurrences,
        InsightsFilter filter,
        TimeZoneInfo timeZone,
        InsightsOptions settings)
    {
        var evaluated = occurrences
            .OrderByDescending(o => o.PeriodStart)
            .Take(settings.ChronicWindowPeriods)
            .ToArray();

        var tally = new ComplianceTally();

        foreach (var occurrence in evaluated)
        {
            tally.Add(occurrence.Status, periodConcluded: true);
        }

        // A late completion is a miss for compliance, so it is a delay here too — this
        // report is "chronically delayed", not "chronically ignored".
        var missed = tally.Missed + tally.Late;

        var lastMissed = evaluated
            .Where(o => o.Status.CountsAsMissed() || o.Status.IsOutstanding())
            .Select(o => (DateOnly?)TenantTime.LocalDate(o.PeriodStart, timeZone))
            .FirstOrDefault();

        // The judged periods are contiguous and every one of them is concluded, so a due-date range
        // over them selects exactly the Evaluated rows and nothing in flight.
        var from = evaluated.Min(o => o.PeriodStart);
        var to = evaluated.Max(o => o.PeriodEnd).AddTicks(-1);

        return new Candidate(
            responsibilityId,
            missed,
            evaluated.Length,
            lastMissed,
            DrillThroughFactory.For(
                InsightsRows.OccurrencesBetween(filter, from, to) with { ResponsibilityId = responsibilityId }));
    }
}
