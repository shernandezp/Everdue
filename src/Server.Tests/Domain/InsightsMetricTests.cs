using Everdue.Server.Application.Common;
using Everdue.Server.Application.Insights;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Insights;

namespace Everdue.Server.Tests.Domain;

/// <summary>
/// The metric contract, tested where it lives: bucketing, the compliance rule and hold arithmetic are
/// pure functions, so they are asserted without a database or a host.
/// </summary>
public class InsightsMetricTests
{
    private static readonly TimeZoneInfo Bogota = TimeZoneLookup.Resolve("America/Bogota");
    private static readonly TimeZoneInfo Madrid = TimeZoneLookup.Resolve("Europe/Madrid");

    [Fact]
    public void Iso_week_buckets_are_monday_based_and_survive_the_year_boundary()
    {
        // ISO week 1 of 2026 starts on Monday 29 December 2025 — the calendar year is not the ISO year.
        var newYear = PeriodBucket.For(BucketKind.Week, new DateOnly(2026, 1, 1));
        newYear.Key.ShouldBe("2026-W01");
        newYear.Label.ShouldBe("W01");
        newYear.Start.ShouldBe(new DateOnly(2025, 12, 29));

        PeriodBucket.For(BucketKind.Week, new DateOnly(2025, 12, 29)).Key.ShouldBe("2026-W01");
        PeriodBucket.For(BucketKind.Week, new DateOnly(2025, 12, 28)).Key.ShouldBe("2025-W52");

        var week31 = PeriodBucket.For(BucketKind.Week, new DateOnly(2026, 7, 28));
        week31.Key.ShouldBe("2026-W31");
        week31.Start.ShouldBe(new DateOnly(2026, 7, 27));
        week31.EndExclusive.ShouldBe(new DateOnly(2026, 8, 3));
        week31.Previous().Key.ShouldBe("2026-W30");
        week31.Next().Key.ShouldBe("2026-W32");
    }

    [Fact]
    public void A_week_bucket_spanning_a_daylight_saving_change_still_covers_seven_local_days()
    {
        // Spain moves its clocks on Sunday 29 March 2026, inside this bucket.
        var bucket = PeriodBucket.For(BucketKind.Week, new DateOnly(2026, 3, 25));
        bucket.Start.ShouldBe(new DateOnly(2026, 3, 23));
        (bucket.EndExclusive.DayNumber - bucket.Start.DayNumber).ShouldBe(7);

        // And the instants those dates denote are still local midnight on both sides of the change.
        var from = TenantTime.StartOfDay(bucket.Start, Madrid);
        var to = TenantTime.StartOfDay(bucket.EndExclusive, Madrid);

        TenantTime.LocalDateTime(from, Madrid).TimeOfDay.ShouldBe(TimeSpan.Zero);
        TenantTime.LocalDateTime(to, Madrid).TimeOfDay.ShouldBe(TimeSpan.Zero);
        (to - from).ShouldBe(TimeSpan.FromDays(7) - TimeSpan.FromHours(1));
    }

    [Fact]
    public void Month_buckets_are_calendar_months()
    {
        var bucket = PeriodBucket.For(BucketKind.Month, new DateOnly(2026, 7, 15));

        bucket.Key.ShouldBe("2026-07");
        bucket.Start.ShouldBe(new DateOnly(2026, 7, 1));
        bucket.EndExclusive.ShouldBe(new DateOnly(2026, 8, 1));
        bucket.Contains(new DateOnly(2026, 7, 31)).ShouldBeTrue();
        bucket.Contains(new DateOnly(2026, 8, 1)).ShouldBeFalse();
    }

    [Fact]
    public void A_series_is_contiguous_and_covers_both_ends()
    {
        var series = PeriodBucket.Series(BucketKind.Week, new DateOnly(2026, 5, 4), new DateOnly(2026, 7, 28));

        series.Count.ShouldBe(13);
        series[0].Start.ShouldBe(new DateOnly(2026, 5, 4));
        series[^1].Key.ShouldBe("2026-W31");
        series.Zip(series.Skip(1)).ShouldAllBe(pair => pair.First.EndExclusive == pair.Second.Start);
    }

    [Fact]
    public void A_period_that_has_ended_counts_as_a_miss_before_any_tick_flips_it()
    {
        var tally = new ComplianceTally();

        tally.Add(WorkItemStatus.Open, periodConcluded: true);
        tally.Add(WorkItemStatus.InProgress, periodConcluded: true);
        tally.Add(WorkItemStatus.OnHold, periodConcluded: true);

        tally.Missed.ShouldBe(3);
        tally.Concluded.ShouldBe(3);
        tally.InFlight.ShouldBe(0);
    }

    [Fact]
    public void Work_whose_period_is_still_running_is_in_flight_even_when_it_is_already_done()
    {
        var tally = new ComplianceTally();

        tally.Add(WorkItemStatus.Completed, periodConcluded: false);
        tally.Add(WorkItemStatus.Open, periodConcluded: false);

        tally.InFlight.ShouldBe(2);
        tally.Concluded.ShouldBe(0);
        tally.Rate(1).ShouldBeNull();
    }

    [Fact]
    public void A_late_completion_counts_against_the_rate_and_inside_the_volume()
    {
        var tally = new ComplianceTally();

        for (var index = 0; index < 26; index++)
        {
            tally.Add(WorkItemStatus.Completed, periodConcluded: true);
        }

        tally.Add(WorkItemStatus.CompletedLate, periodConcluded: true);
        tally.Add(WorkItemStatus.Missed, periodConcluded: true);
        tally.Add(WorkItemStatus.Missed, periodConcluded: true);
        tally.Add(WorkItemStatus.Missed, periodConcluded: true);

        tally.OnTime.ShouldBe(26);
        tally.Late.ShouldBe(1);
        tally.Missed.ShouldBe(3);
        tally.Concluded.ShouldBe(30);
        Math.Round(tally.Rate(5)!.Value * 100).ShouldBe(87);
    }

    [Fact]
    public void A_rate_on_a_thin_denominator_is_withheld_rather_than_shown()
    {
        var tally = new ComplianceTally();
        tally.Add(WorkItemStatus.Completed, periodConcluded: true);
        tally.Add(WorkItemStatus.Missed, periodConcluded: true);
        tally.Add(WorkItemStatus.Missed, periodConcluded: true);

        tally.Rate(5).ShouldBeNull();
        tally.IsSuppressed(5).ShouldBeTrue();

        // Nothing to divide is not the same thing as too little to divide.
        var empty = new ComplianceTally();
        empty.Rate(5).ShouldBeNull();
        empty.IsSuppressed(5).ShouldBeFalse();
    }

    [Fact]
    public void A_hold_is_clipped_to_the_window_it_is_reported_in()
    {
        var start = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        var interval = new HoldInterval(Guid.CreateVersion7(), HoldReason.WaitingCustomer, start, start.AddDays(10), Open: false);

        var inside = interval.Clip(start.AddDays(2), start.AddDays(5));
        inside.ShouldNotBeNull();
        inside!.Value.Days.ShouldBe(3);

        interval.Clip(start.AddDays(-10), start.AddDays(-5)).ShouldBeNull();
        interval.Clip(start.AddDays(20), start.AddDays(30)).ShouldBeNull();
        interval.Clip(start, start).ShouldBeNull();

        interval.Overlaps(start.AddDays(9), start.AddDays(11)).ShouldBeTrue();
        interval.Overlaps(start.AddDays(10), start.AddDays(11)).ShouldBeFalse();
    }

    [Fact]
    public void The_default_window_is_twelve_buckets_ending_with_the_one_in_progress()
    {
        var options = new InsightsOptions();
        var now = DateTimeOffset.Parse("2026-07-28T15:00:00Z"); // 10:00 in Bogota

        var window = InsightsWindow.Resolve(BucketKind.Week, null, null, null, Bogota, now, options);

        window.Buckets.Count.ShouldBe(12);
        window.Buckets[^1].Key.ShouldBe("2026-W31");
        window.Buckets[0].Key.ShouldBe("2026-W20");
        window.IsPartial(window.Buckets[^1]).ShouldBeTrue();
        window.IsPartial(window.Buckets[^2]).ShouldBeFalse();

        // Both boundaries are local midnight — the property the drill-throughs depend on.
        window.From.ShouldBe(TenantTime.StartOfDay(new DateOnly(2026, 5, 11), Bogota));
        window.To.ShouldBe(TenantTime.StartOfDay(new DateOnly(2026, 8, 3), Bogota));
        window.EffectiveTo(now).ShouldBe(now);
    }

    [Fact]
    public void An_over_wide_calendar_range_is_refused_by_name_instead_of_truncated()
    {
        var options = new InsightsOptions();
        var now = DateTimeOffset.Parse("2026-07-28T15:00:00Z");

        // Clamped, not rejected: an absurd rolling count is a typo, not a request for four years.
        var clamped = InsightsWindow.Resolve(BucketKind.Week, null, null, 999, Bogota, now, options);
        clamped.Buckets.Count.ShouldBe(options.MaxTrendBuckets);

        var tooWide = () => InsightsWindow.Resolve(
            BucketKind.Week,
            DateTimeOffset.Parse("2020-01-01T00:00:00Z"),
            now,
            null,
            Bogota,
            now,
            options);

        var problem = tooWide.ShouldThrow<ValidationException>();
        problem.Message.ShouldContain("52");
    }

    [Fact]
    public void A_calendar_window_buckets_the_range_it_was_given()
    {
        var options = new InsightsOptions();
        var now = DateTimeOffset.Parse("2026-07-28T15:00:00Z");

        var window = InsightsWindow.Resolve(
            BucketKind.Month,
            DateTimeOffset.Parse("2026-01-10T00:00:00Z"),
            DateTimeOffset.Parse("2026-03-20T00:00:00Z"),
            null,
            Bogota,
            now,
            options);

        window.Buckets.Select(b => b.Key).ShouldBe(["2026-01", "2026-02", "2026-03"]);
        window.From.ShouldBe(TenantTime.StartOfDay(new DateOnly(2026, 1, 1), Bogota));
        window.To.ShouldBe(TenantTime.StartOfDay(new DateOnly(2026, 4, 1), Bogota));

        // A window that ended in the past never reports "now" as its end.
        window.EffectiveTo(now).ShouldBe(window.To);
    }

    [Fact]
    public void An_end_date_without_a_start_counts_the_same_number_of_buckets_backwards()
    {
        var options = new InsightsOptions();
        var now = DateTimeOffset.Parse("2026-07-28T15:00:00Z");

        var window = InsightsWindow.Resolve(
            BucketKind.Month,
            from: null,
            to: DateTimeOffset.Parse("2026-05-20T12:00:00Z"),
            requestedBuckets: 4,
            Bogota,
            now,
            options);

        // Four buckets ending with the one the given date falls in — the rolling rule, with a
        // different end. Nothing partial, because that month is over.
        window.Buckets.Select(b => b.Key).ShouldBe(["2026-02", "2026-03", "2026-04", "2026-05"]);
        window.Buckets.ShouldAllBe(bucket => !window.IsPartial(bucket));
    }
}
