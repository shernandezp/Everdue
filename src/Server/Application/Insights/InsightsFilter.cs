using Everdue.Server.Application.Common;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Insights;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// The scope filters every insight endpoint accepts. Deliberately its own type rather than a reuse of
/// <c>ReportFilter</c>: the insight surface additionally scopes by a single entity, and widening the
/// existing type would change four shipped report handlers to serve six new ones.
/// </summary>
public sealed record InsightsFilter(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? EntityId = null,
    EntityType? EntityType = null,

    /// <summary>Set only by the per-responsibility page, so its read is one responsibility wide.</summary>
    Guid? ResponsibilityId = null);

/// <summary>
/// The resolved reporting window: an instant range, plus the dense bucket axis that covers exactly
/// that range.
///
/// Both boundaries are snapped to **tenant-local midnight**. That is what keeps three things in
/// agreement: the bucket axis, the SQL predicate on <c>PeriodStart</c>, and the drill-through's
/// predicate on <c>DueDate</c> — an occurrence's period start is local 00:00 of its scheduled day and
/// its due date is local 23:59:59 of the same day, so with midnight boundaries both filters select
/// exactly the same rows. Without the snap, a number and the list behind it could differ by an
/// occurrence at either edge.
/// </summary>
public sealed record InsightsWindow(
    BucketKind Kind,
    DateTimeOffset From,
    DateTimeOffset To,
    DateOnly Today,
    TimeZoneInfo TimeZone,
    IReadOnlyList<PeriodBucket> Buckets)
{
    /// <summary>The end of the window as far as measurement goes — never the future.</summary>
    public DateTimeOffset EffectiveTo(DateTimeOffset now) => To < now ? To : now;

    public bool IsPartial(PeriodBucket bucket) => bucket.Contains(Today);

    /// <summary>The bucket key an instant belongs to, in tenant-local terms.</summary>
    public string KeyFor(DateTimeOffset instant)
        => PeriodBucket.For(Kind, TenantTime.LocalDate(instant, TimeZone)).Key;

    /// <summary>
    /// Rolling (the default) is the last <c>buckets</c> buckets, the newest of which is the one still in
    /// progress; passing from/to switches to calendar mode over that range instead.
    ///
    /// <c>buckets</c> counts the axis, not the history behind it: asking for twelve returns twelve
    /// columns. An axis whose length is one more than the number requested would be a surprise in every
    /// caller, and the partial column is marked as partial anyway.
    /// </summary>
    public static InsightsWindow Resolve(
        BucketKind kind,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? requestedBuckets,
        TimeZoneInfo timeZone,
        DateTimeOffset now,
        InsightsOptions options)
    {
        var today = TenantTime.LocalDate(now, timeZone);
        var count = Math.Clamp(requestedBuckets ?? options.DefaultTrendBuckets, 1, options.MaxTrendBuckets);

        var lastDate = from is null && to is null ? today : TenantTime.LocalDate(to ?? now, timeZone);

        // A missing 'from' means "count back from the end", in either mode: the rolling window is the
        // same computation with today as the end, so there is one rule rather than two. An explicit
        // 'from' defines the range outright, and `buckets` has nothing left to say.
        var firstDate = from is { } start ? TenantTime.LocalDate(start, timeZone) : Rewind(kind, lastDate, count);

        if (firstDate > lastDate)
        {
            throw new ValidationException("'from' must not be later than 'to'.");
        }

        var buckets = PeriodBucket.Series(kind, firstDate, lastDate);

        if (buckets.Count > options.MaxTrendBuckets)
        {
            // Truncating silently would report a partial window as a whole one. Refusing names the
            // limit and the two ways out.
            throw new ValidationException(
                $"That range needs {buckets.Count} {kind.ToString().ToLowerInvariant()} buckets and the limit is "
                + $"{options.MaxTrendBuckets}. Narrow the range or ask for bucket=Month.");
        }

        return new InsightsWindow(
            kind,
            TenantTime.StartOfDay(buckets[0].Start, timeZone),
            TenantTime.StartOfDay(buckets[^1].EndExclusive, timeZone),
            today,
            timeZone,
            buckets);
    }

    /// <summary>The start of the bucket <paramref name="count"/> - 1 buckets before the one containing <paramref name="date"/>.</summary>
    private static DateOnly Rewind(BucketKind kind, DateOnly date, int count)
    {
        var bucket = PeriodBucket.For(kind, date);

        for (var step = 1; step < count; step++)
        {
            bucket = bucket.Previous();
        }

        return bucket.Start;
    }
}
