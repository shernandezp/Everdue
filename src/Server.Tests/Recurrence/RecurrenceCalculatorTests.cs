using Everdue.Server.Domain;
using Everdue.Server.Domain.Recurrence;

namespace Everdue.Server.Tests.Recurrence;

/// <summary>
/// The largest suite in the repository, on purpose: everything the ledger asserts about a period
/// boundary is downstream of this pure function.
/// </summary>
public class RecurrenceCalculatorTests
{
    private static RecurrenceRule Daily(string start = "2026-01-01")
        => new(RecurrenceKind.Daily, null, null, null, DateOnly.Parse(start));

    private static RecurrenceRule Weekly(int mask, string start = "2026-01-01")
        => new(RecurrenceKind.WeeklyOnDays, mask, null, null, DateOnly.Parse(start));

    private static RecurrenceRule Monthly(int day, string start = "2026-01-01")
        => new(RecurrenceKind.MonthlyOnDay, null, day, null, DateOnly.Parse(start));

    private static RecurrenceRule Yearly(int month, int day, string start = "2026-01-01")
        => new(RecurrenceKind.Yearly, null, day, month, DateOnly.Parse(start));

    private static DateOnly D(string value) => DateOnly.Parse(value);

    [Fact]
    public void Daily_advances_one_calendar_day()
    {
        RecurrenceCalculator.NextScheduledDate(Daily(), D("2026-03-14")).ShouldBe(D("2026-03-15"));
    }

    [Fact]
    public void Daily_crosses_a_month_and_a_year_boundary()
    {
        RecurrenceCalculator.NextScheduledDate(Daily(), D("2026-01-31")).ShouldBe(D("2026-02-01"));
        RecurrenceCalculator.NextScheduledDate(Daily(), D("2026-12-31")).ShouldBe(D("2027-01-01"));
    }

    [Theory]
    // Monday-only: from each weekday the next Monday is the following one.
    [InlineData("2026-07-27", "2026-08-03")] // Monday  -> next Monday
    [InlineData("2026-07-28", "2026-08-03")] // Tuesday -> next Monday
    [InlineData("2026-08-02", "2026-08-03")] // Sunday  -> tomorrow
    public void WeeklyOnDays_finds_the_next_selected_weekday(string after, string expected)
    {
        var rule = Weekly(RecurrenceRule.MaskFor(DayOfWeek.Monday));
        RecurrenceCalculator.NextScheduledDate(rule, D(after)).ShouldBe(D(expected));
    }

    [Fact]
    public void WeeklyOnDays_with_several_days_runs_selected_day_to_next_selected_day()
    {
        var rule = Weekly(RecurrenceRule.MaskFor(DayOfWeek.Monday, DayOfWeek.Thursday));

        // Mon 27 Jul 2026 -> Thu 30 -> Mon 3 Aug: the periods are uneven, which is exactly the point.
        RecurrenceCalculator.NextScheduledDate(rule, D("2026-07-27")).ShouldBe(D("2026-07-30"));
        RecurrenceCalculator.NextScheduledDate(rule, D("2026-07-30")).ShouldBe(D("2026-08-03"));
    }

    [Fact]
    public void WeeklyOnDays_with_every_day_selected_behaves_like_daily()
    {
        var rule = Weekly(0b111_1111);
        RecurrenceCalculator.NextScheduledDate(rule, D("2026-07-27")).ShouldBe(D("2026-07-28"));
    }

    [Theory]
    [InlineData(31, "2026-01-31", "2026-02-28")] // clamps to February in a common year
    [InlineData(31, "2026-02-28", "2026-03-31")] // and un-clamps the month after
    [InlineData(31, "2026-04-30", "2026-05-31")]
    [InlineData(30, "2026-01-30", "2026-02-28")]
    [InlineData(15, "2026-01-15", "2026-02-15")]
    [InlineData(1, "2026-12-01", "2027-01-01")]
    public void MonthlyOnDay_clamps_to_the_last_day_of_short_months(int day, string after, string expected)
    {
        RecurrenceCalculator.NextScheduledDate(Monthly(day), D(after)).ShouldBe(D(expected));
    }

    [Fact]
    public void MonthlyOnDay_31_clamps_to_29_February_in_a_leap_year()
    {
        RecurrenceCalculator.NextScheduledDate(Monthly(31), D("2028-01-31")).ShouldBe(D("2028-02-29"));
    }

    [Theory]
    [InlineData(2, 29, "2026-02-28", "2027-02-28")] // 29 Feb clamps to the 28th in common years
    [InlineData(2, 29, "2027-02-28", "2028-02-29")] // and is itself in a leap year
    [InlineData(7, 4, "2026-07-04", "2027-07-04")]
    [InlineData(1, 1, "2026-06-01", "2027-01-01")]
    public void Yearly_clamps_and_advances(int month, int day, string after, string expected)
    {
        RecurrenceCalculator.NextScheduledDate(Yearly(month, day), D(after)).ShouldBe(D(expected));
    }

    [Fact]
    public void FirstScheduledDate_is_the_first_scheduled_date_on_or_after_StartDate()
    {
        // StartDate itself is a Monday, so it is the first occurrence — no back-fill, no skip.
        RecurrenceCalculator.FirstScheduledDate(Weekly(RecurrenceRule.MaskFor(DayOfWeek.Monday), "2026-07-27"))
            .ShouldBe(D("2026-07-27"));

        // StartDate is a Tuesday, so the series starts on the following Monday.
        RecurrenceCalculator.FirstScheduledDate(Weekly(RecurrenceRule.MaskFor(DayOfWeek.Monday), "2026-07-28"))
            .ShouldBe(D("2026-08-03"));

        // A responsibility created mid-month with day 15 starts next month, not retroactively.
        RecurrenceCalculator.FirstScheduledDate(Monthly(15, "2026-07-20")).ShouldBe(D("2026-08-15"));
    }

    [Fact]
    public void IsScheduled_agrees_with_the_walk()
    {
        var rule = Monthly(31);
        var cursor = RecurrenceCalculator.FirstScheduledDate(rule);

        for (var i = 0; i < 36; i++)
        {
            RecurrenceCalculator.IsScheduled(rule, cursor).ShouldBeTrue($"{cursor:O} should be scheduled");
            var next = RecurrenceCalculator.NextScheduledDate(rule, cursor);
            next.ShouldBeGreaterThan(cursor);
            cursor = next;
        }
    }

    [Fact]
    public void ScheduledDatesAfter_is_strictly_increasing_and_never_repeats()
    {
        var rule = Weekly(RecurrenceRule.MaskFor(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday));
        var dates = RecurrenceCalculator.ScheduledDatesAfter(rule, D("2026-01-01")).Take(60).ToArray();

        dates.Distinct().Count().ShouldBe(dates.Length);
        dates.Zip(dates.Skip(1)).ShouldAllBe(pair => pair.Second > pair.First);
        dates.ShouldAllBe(d => d.DayOfWeek == DayOfWeek.Monday || d.DayOfWeek == DayOfWeek.Wednesday || d.DayOfWeek == DayOfWeek.Friday);
    }

    [Theory]
    [InlineData(RecurrenceKind.WeeklyOnDays, null, null, null)]  // no weekdays selected
    [InlineData(RecurrenceKind.MonthlyOnDay, null, null, null)]  // no day of month
    [InlineData(RecurrenceKind.MonthlyOnDay, null, 32, null)]    // impossible day
    [InlineData(RecurrenceKind.Yearly, null, 30, 2)]             // 30 February never exists
    [InlineData(RecurrenceKind.Yearly, null, null, 13)]          // no such month
    public void Validate_rejects_unusable_rules(RecurrenceKind kind, int? mask, int? day, int? month)
    {
        new RecurrenceRule(kind, mask, day, month, D("2026-01-01")).Validate().ShouldNotBeNull();
    }

    [Fact]
    public void Validate_accepts_29_February_because_it_clamps()
    {
        new RecurrenceRule(RecurrenceKind.Yearly, null, 29, 2, D("2026-01-01")).Validate().ShouldBeNull();
    }

    [Fact]
    public void MaskFor_and_DaysFromMask_round_trip()
    {
        var days = new[] { DayOfWeek.Sunday, DayOfWeek.Wednesday, DayOfWeek.Saturday };
        RecurrenceRule.DaysFromMask(RecurrenceRule.MaskFor(days)).ShouldBe(days);
    }
}
