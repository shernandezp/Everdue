using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain.Insights;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// Turns a set of occurrences into one tally plus a dense trend, so the compliance table, the
/// per-responsibility page and the reliability report all count the same way. The invariant this
/// exists to hold: the trend's counts sum to the headline's counts, always.
/// </summary>
internal static class ComplianceCalculator
{
    internal sealed record Result(ComplianceTally Tally, IReadOnlyList<BucketPointDto> Trend);

    public static Result Build(IEnumerable<LedgerOccurrence> occurrences, InsightsWindow window, DateTimeOffset now)
    {
        var total = new ComplianceTally();
        var byBucket = new Dictionary<string, ComplianceTally>();

        foreach (var occurrence in occurrences)
        {
            var concluded = occurrence.IsConcludedAt(now);
            total.Add(occurrence.Status, concluded);

            var key = window.KeyFor(occurrence.PeriodStart);

            if (!byBucket.TryGetValue(key, out var bucket))
            {
                byBucket[key] = bucket = new ComplianceTally();
            }

            bucket.Add(occurrence.Status, concluded);
        }

        var trend = window.Buckets
            .Select(bucket =>
            {
                var tally = byBucket.GetValueOrDefault(bucket.Key) ?? new ComplianceTally();

                return new BucketPointDto(
                    bucket.Key,
                    bucket.Label,
                    bucket.Start,
                    window.IsPartial(bucket),
                    tally.OnTime,
                    tally.Late,
                    tally.Missed,

                    // A point's rate is raw, never suppressed: a trend line shows shape, and a
                    // suppression threshold applied per week would blank out almost every point of it.
                    // The headline rate beside it is the one that gets withheld when the volume is thin.
                    tally.Concluded == 0 ? null : (double)tally.OnTime / tally.Concluded);
            })
            .ToArray();

        return new Result(total, trend);
    }
}
