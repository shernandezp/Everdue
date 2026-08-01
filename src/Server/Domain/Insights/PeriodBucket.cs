using System.Globalization;

namespace Everdue.Server.Domain.Insights;

public enum BucketKind
{
    Week = 0,
    Month = 1,
}

/// <summary>
/// One column of a trend axis, expressed in **civil local dates** — never in instants.
///
/// Bucketing is done here rather than in SQL because a tenant-local ISO week cannot be derived
/// portably from a UTC timestamp across SQLite and PostgreSQL, and the dual-provider rule forbids
/// provider-specific SQL. The caller converts an instant to a local date with
/// <see cref="TenantTime.LocalDate"/> first and asks for the bucket that contains it.
/// </summary>
public readonly record struct PeriodBucket(BucketKind Kind, DateOnly Start, DateOnly EndExclusive, string Key, string Label)
{
    /// <summary>The bucket that contains <paramref name="date"/>.</summary>
    public static PeriodBucket For(BucketKind kind, DateOnly date)
        => kind == BucketKind.Month ? Month(date) : Week(date);

    public bool Contains(DateOnly date) => date >= Start && date < EndExclusive;

    public PeriodBucket Next() => For(Kind, EndExclusive);

    public PeriodBucket Previous() => For(Kind, Start.AddDays(-1));

    /// <summary>
    /// Every bucket from the one containing <paramref name="fromDate"/> to the one containing
    /// <paramref name="toDate"/>, inclusive and contiguous. Dense by construction: a trend chart must
    /// never imply that a quiet month is a missing one.
    /// </summary>
    public static IReadOnlyList<PeriodBucket> Series(BucketKind kind, DateOnly fromDate, DateOnly toDate)
    {
        var first = For(kind, fromDate);

        if (toDate < fromDate)
        {
            return [first];
        }

        var buckets = new List<PeriodBucket>();
        var cursor = first;

        // The guard is a runaway backstop, not a product limit — the Application layer refuses a
        // range that implies more buckets than it is willing to return, and says so.
        for (var guard = 0; guard < 10_000; guard++)
        {
            buckets.Add(cursor);

            if (cursor.Contains(toDate))
            {
                break;
            }

            cursor = cursor.Next();
        }

        return buckets;
    }

    /// <summary>ISO-8601 week, Monday-based — the "Week 29 ✅ / Week 30 ❌" vocabulary the product already speaks.</summary>
    private static PeriodBucket Week(DateOnly date)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);
        var isoYear = ISOWeek.GetYear(day);
        var isoWeek = ISOWeek.GetWeekOfYear(day);

        var monday = DateOnly.FromDateTime(ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday));

        return new PeriodBucket(
            BucketKind.Week,
            monday,
            monday.AddDays(7),
            string.Create(CultureInfo.InvariantCulture, $"{isoYear:0000}-W{isoWeek:00}"),
            string.Create(CultureInfo.InvariantCulture, $"W{isoWeek:00}"));
    }

    private static PeriodBucket Month(DateOnly date)
    {
        var first = new DateOnly(date.Year, date.Month, 1);
        var key = string.Create(CultureInfo.InvariantCulture, $"{first.Year:0000}-{first.Month:00}");

        return new PeriodBucket(BucketKind.Month, first, first.AddMonths(1), key, key);
    }
}
