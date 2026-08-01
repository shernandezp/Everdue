using Everdue.Server.Domain;
using Everdue.Server.Domain.Recurrence;

namespace Everdue.Server.Tests.Recurrence;

/// <summary>
/// Period boundaries are computed as civil local dates and only then converted, so a DST shift can
/// never move a period off local midnight. These are the tests that prove it.
/// </summary>
public class PeriodAndTimeZoneTests
{
    private static readonly TimeZoneInfo Bogota = TimeZoneLookup.Resolve("America/Bogota");     // no DST
    private static readonly TimeZoneInfo NewYork = TimeZoneLookup.Resolve("America/New_York");  // spring forward at 02:00
    private static readonly TimeZoneInfo Santiago = TimeZoneLookup.Resolve("America/Santiago"); // spring forward at midnight

    private static DateOnly D(string value) => DateOnly.Parse(value);

    [Fact]
    public void StartOfDay_is_local_midnight_expressed_in_utc()
    {
        // Bogota is UTC-5 all year.
        TenantTime.StartOfDay(D("2026-07-27"), Bogota).ShouldBe(DateTimeOffset.Parse("2026-07-27T05:00:00Z"));
        TenantTime.EndOfDay(D("2026-07-27"), Bogota).ShouldBe(DateTimeOffset.Parse("2026-07-28T04:59:59Z"));
    }

    [Fact]
    public void Spring_forward_still_yields_exactly_one_midnight_per_day()
    {
        // New York springs forward at 02:00 on 8 March 2026: midnight exists, the offset changes later.
        var before = TenantTime.StartOfDay(D("2026-03-07"), NewYork);
        var during = TenantTime.StartOfDay(D("2026-03-08"), NewYork);
        var after = TenantTime.StartOfDay(D("2026-03-09"), NewYork);

        (during - before).ShouldBe(TimeSpan.FromHours(24));
        (after - during).ShouldBe(TimeSpan.FromHours(23)); // the short day, as it should be
    }

    [Fact]
    public void A_local_midnight_that_does_not_exist_resolves_to_the_earliest_valid_instant()
    {
        // Santiago skips 00:00-01:00 on the spring-forward Sunday; the day begins at 01:00 local.
        var springForward = FindSantiagoSpringForwardDate();
        var start = TenantTime.StartOfDay(springForward, Santiago);

        var local = TimeZoneInfo.ConvertTime(start, Santiago);
        local.Hour.ShouldBe(1);
        local.Date.ShouldBe(springForward.ToDateTime(TimeOnly.MinValue));
    }

    [Fact]
    public void An_ambiguous_local_time_resolves_to_the_first_of_the_two_passes()
    {
        // Chile's fall-back happens at midnight, so it is the hour *before* midnight that repeats,
        // not midnight itself. Search for whichever local hour is ambiguous rather than assuming.
        var ambiguous = FindSantiagoAmbiguousLocalTime();

        var instant = TenantTime.ToInstant(ambiguous, Santiago);

        // Instants are always returned in UTC, so compare the moment rather than the offset:
        // of the two moments that spell this local time, we must pick the earlier one.
        var candidates = Santiago.GetAmbiguousTimeOffsets(ambiguous)
            .Select(offset => new DateTimeOffset(ambiguous, offset).ToUniversalTime())
            .ToArray();

        candidates.Length.ShouldBe(2);
        instant.ShouldBe(candidates.Min());
        TimeZoneInfo.ConvertTime(instant, Santiago).DateTime.ShouldBe(ambiguous);
    }

    [Fact]
    public void LocalDate_round_trips_through_StartOfDay()
    {
        for (var day = 0; day < 400; day++)
        {
            var date = D("2026-01-01").AddDays(day);
            TenantTime.LocalDate(TenantTime.StartOfDay(date, Santiago), Santiago).ShouldBe(date);
        }
    }

    [Fact]
    public void Occurrence_period_ends_exactly_where_the_next_one_begins()
    {
        var rule = new RecurrenceRule(RecurrenceKind.WeeklyOnDays, RecurrenceRule.MaskFor(DayOfWeek.Monday), null, null, D("2026-01-01"));

        var first = OccurrencePeriod.For(rule, D("2026-07-27"), Bogota);
        var second = OccurrencePeriod.For(rule, D("2026-08-03"), Bogota);

        // No gap and no overlap: the successor spawns at the instant its predecessor is missed.
        first.PeriodEnd.ShouldBe(second.PeriodStart);
        first.DueDate.ShouldBeLessThan(first.PeriodEnd);
        first.DueDate.ShouldBeGreaterThan(first.PeriodStart);
    }

    [Fact]
    public void Daily_periods_are_one_day_long_even_across_a_DST_change()
    {
        var rule = new RecurrenceRule(RecurrenceKind.Daily, null, null, null, D("2026-01-01"));
        var springForward = FindSantiagoSpringForwardDate();

        var period = OccurrencePeriod.For(rule, springForward.AddDays(-1), Santiago);

        // The civil day is still "the day", even though the elapsed time is 23 hours.
        TenantTime.LocalDate(period.PeriodStart, Santiago).ShouldBe(springForward.AddDays(-1));
        TenantTime.LocalDate(period.PeriodEnd, Santiago).ShouldBe(springForward);
    }

    [Fact]
    public void Unknown_time_zone_ids_are_rejected_rather_than_silently_becoming_utc()
    {
        TimeZoneLookup.IsKnown("America/Bogota").ShouldBeTrue();
        TimeZoneLookup.IsKnown("Mars/Olympus_Mons").ShouldBeFalse();
        Should.Throw<TimeZoneNotFoundException>(() => TimeZoneLookup.Resolve("Mars/Olympus_Mons"));
    }

    /// <summary>
    /// Found by search rather than hard-coded: Chile has moved its transition dates repeatedly, and a
    /// test that pinned one would rot the next time the tz database is updated.
    /// </summary>
    private static DateOnly FindSantiagoSpringForwardDate()
    {
        for (var day = 0; day < 730; day++)
        {
            var date = new DateOnly(2026, 1, 1).AddDays(day);
            if (Santiago.IsInvalidTime(date.ToDateTime(TimeOnly.MinValue)))
            {
                return date;
            }
        }

        throw new InvalidOperationException("No spring-forward midnight found for America/Santiago in 2026-2027.");
    }

    private static DateTime FindSantiagoAmbiguousLocalTime()
    {
        for (var hour = 0; hour < 730 * 24; hour++)
        {
            var candidate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddHours(hour);
            if (Santiago.IsAmbiguousTime(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No ambiguous local time found for America/Santiago in 2026-2027.");
    }
}
