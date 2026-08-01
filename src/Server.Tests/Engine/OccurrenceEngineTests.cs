using Everdue.Server.Domain;
using Everdue.Server.Domain.Recurrence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Tests.Engine;

/// <summary>
/// The engine's contract in full: it spawns regardless of what happened last period, it records the
/// miss, and it never stalls a series. Everything here runs against the real schema.
/// </summary>
public class OccurrenceEngineTests
{
    private static readonly int Mondays = RecurrenceRule.MaskFor(DayOfWeek.Monday);

    private static DateOnly D(string value) => DateOnly.Parse(value);

    [Fact]
    public async Task A_fresh_responsibility_spawns_the_current_period_only()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-28T15:00:00Z"); // Tuesday afternoon in Bogota

        var responsibility = harness.AddResponsibility(RecurrenceKind.WeeklyOnDays, D("2026-07-27"), daysOfWeekMask: Mondays);

        await harness.Engine().TickAsync();

        var occurrences = await harness.OccurrencesAsync(responsibility.Id);
        occurrences.Count.ShouldBe(1);
        occurrences[0].Status.ShouldBe(WorkItemStatus.Open);
        TenantTime.LocalDate(occurrences[0].PeriodStart!.Value, harness.TimeZone).ShouldBe(D("2026-07-27"));
        TenantTime.LocalDate(occurrences[0].PeriodEnd!.Value, harness.TimeZone).ShouldBe(D("2026-08-03"));
    }

    [Fact]
    public async Task A_responsibility_created_mid_period_does_not_back_fill_before_its_StartDate()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-28T15:00:00Z");

        // Starts Wednesday; the rule fires on Mondays, so the first occurrence is next Monday.
        var responsibility = harness.AddResponsibility(RecurrenceKind.WeeklyOnDays, D("2026-07-29"), daysOfWeekMask: Mondays);

        await harness.Engine().TickAsync();

        (await harness.OccurrencesAsync(responsibility.Id)).ShouldBeEmpty();

        harness.Clock.Set("2026-08-03T15:00:00Z");
        await harness.Engine().TickAsync();

        var occurrences = await harness.OccurrencesAsync(responsibility.Id);
        occurrences.Count.ShouldBe(1);
        TenantTime.LocalDate(occurrences[0].PeriodStart!.Value, harness.TimeZone).ShouldBe(D("2026-08-03"));
    }

    /// <summary>Acceptance criterion 1: three weeks untouched is three Missed rows and one Open.</summary>
    [Fact]
    public async Task Three_untouched_weeks_produce_exactly_three_missed_and_one_open()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-28T15:00:00Z");

        var responsibility = harness.AddResponsibility(RecurrenceKind.WeeklyOnDays, D("2026-07-06"), daysOfWeekMask: Mondays);

        await harness.Engine().TickAsync();

        var occurrences = await harness.OccurrencesAsync(responsibility.Id);

        occurrences.Count(o => o.Status == WorkItemStatus.Missed).ShouldBe(3);
        occurrences.Count(o => o.Status == WorkItemStatus.Open).ShouldBe(1);

        occurrences.Select(o => TenantTime.LocalDate(o.PeriodStart!.Value, harness.TimeZone))
            .ShouldBe([D("2026-07-06"), D("2026-07-13"), D("2026-07-20"), D("2026-07-27")]);

        // Local due date is 23:59:59 of the scheduled day, in the tenant's zone.
        var due = TenantTime.LocalDateTime(occurrences[0].DueDate, harness.TimeZone);
        due.ShouldBe(new DateTime(2026, 7, 6, 23, 59, 59));
    }

    /// <summary>Acceptance criterion 3: downtime must be invisible in the ledger.</summary>
    [Fact]
    public async Task Fourteen_days_of_downtime_produces_the_same_ledger_as_running_continuously()
    {
        var start = D("2026-07-01");
        var end = "2026-07-15T09:00:00Z";

        // (a) A server that ticked every day right through the fortnight.
        await using var continuous = await EngineHarness.CreateAsync();
        continuous.Clock.Set("2026-07-01T09:00:00Z");
        var continuousResponsibility = continuous.AddResponsibility(RecurrenceKind.Daily, start);

        while (continuous.Clock.UtcNow <= DateTimeOffset.Parse(end))
        {
            await continuous.Engine().TickAsync();
            continuous.Clock.AdvanceDays(1);
        }

        // (b) A server that was off the whole time and started once, at the end.
        await using var restarted = await EngineHarness.CreateAsync();
        restarted.Clock.Set(end);
        var restartedResponsibility = restarted.AddResponsibility(RecurrenceKind.Daily, start);
        await restarted.Engine().TickAsync();

        var a = await continuous.OccurrencesAsync(continuousResponsibility.Id);
        var b = await restarted.OccurrencesAsync(restartedResponsibility.Id);

        a.Count.ShouldBe(b.Count);
        a.Select(o => (o.PeriodStart, o.PeriodEnd, o.DueDate, o.Status))
            .ShouldBe(b.Select(o => (o.PeriodStart, o.PeriodEnd, o.DueDate, o.Status)));

        // 1-14 July are all past; the 15th is still open.
        b.Count(o => o.Status == WorkItemStatus.Missed).ShouldBe(14);
        b.Count(o => o.Status == WorkItemStatus.Open).ShouldBe(1);
    }

    [Fact]
    public async Task Two_concurrent_ticks_create_no_duplicates()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-28T15:00:00Z");

        var responsibility = harness.AddResponsibility(RecurrenceKind.Daily, D("2026-07-20"));

        // Two independent contexts on the same database: neither sees the other's pending inserts,
        // so the unique index is the only thing standing between us and a duplicated ledger.
        var first = harness.NewContext();
        var second = harness.NewContext();

        await Task.WhenAll(
            harness.EngineOn(first).TickAsync(),
            harness.EngineOn(second).TickAsync());

        var occurrences = await harness.OccurrencesAsync(responsibility.Id);
        occurrences.Select(o => o.PeriodStart).Distinct().Count().ShouldBe(occurrences.Count);
        occurrences.Count.ShouldBe(9); // 20-27 July missed, 28 July open
    }

    [Fact]
    public async Task Ticking_repeatedly_is_idempotent()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-28T15:00:00Z");

        var responsibility = harness.AddResponsibility(RecurrenceKind.Daily, D("2026-07-25"));

        await harness.Engine().TickAsync();
        var afterFirst = await harness.OccurrencesAsync(responsibility.Id);

        await harness.Engine().TickAsync();
        await harness.Engine().TickAsync();

        var afterThird = await harness.OccurrencesAsync(responsibility.Id);
        afterThird.Count.ShouldBe(afterFirst.Count);
    }

    [Fact]
    public async Task An_open_occurrence_flips_to_missed_when_its_period_ends()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-27T15:00:00Z");

        var responsibility = harness.AddResponsibility(RecurrenceKind.WeeklyOnDays, D("2026-07-27"), daysOfWeekMask: Mondays);
        await harness.Engine().TickAsync();

        (await harness.OccurrencesAsync(responsibility.Id)).Single().Status.ShouldBe(WorkItemStatus.Open);

        harness.Clock.Set("2026-08-03T05:00:01Z"); // one second past the period end in Bogota
        await harness.Engine().TickAsync();

        var occurrences = await harness.OccurrencesAsync(responsibility.Id);
        occurrences.Count.ShouldBe(2);
        occurrences[0].Status.ShouldBe(WorkItemStatus.Missed);
        occurrences[1].Status.ShouldBe(WorkItemStatus.Open); // the successor spawned regardless
    }

    [Fact]
    public async Task An_on_hold_occurrence_still_misses_and_the_event_records_the_prior_status()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-27T15:00:00Z");

        var responsibility = harness.AddResponsibility(RecurrenceKind.WeeklyOnDays, D("2026-07-27"), daysOfWeekMask: Mondays);
        await harness.Engine().TickAsync();

        var occurrence = await harness.Db.WorkItems.FirstAsync(w => w.ResponsibilityId == responsibility.Id);
        occurrence.Status = WorkItemStatus.OnHold;
        occurrence.HoldReason = HoldReason.WaitingCustomer;
        await harness.Db.SaveChangesAsync();

        harness.Clock.Set("2026-08-03T06:00:00Z");
        await harness.Engine().TickAsync();

        var reloaded = await harness.Db.WorkItems.AsNoTracking().FirstAsync(w => w.Id == occurrence.Id);
        reloaded.Status.ShouldBe(WorkItemStatus.Missed);

        var missEvent = await harness.Db.WorkItemEvents.AsNoTracking()
            .Where(e => e.WorkItemId == occurrence.Id && e.ToStatus == WorkItemStatus.Missed)
            .SingleAsync();

        missEvent.UserId.ShouldBeNull(); // written by the engine
        missEvent.FromStatus.ShouldBe(WorkItemStatus.OnHold);
        missEvent.DataJson.ShouldNotBeNull();
        missEvent.DataJson!.ShouldContain("OnHold");
        missEvent.DataJson.ShouldContain("WaitingCustomer");
    }

    [Fact]
    public async Task A_paused_responsibility_spawns_nothing_and_the_paused_periods_are_skipped_not_missed()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-01T09:00:00Z");

        var responsibility = harness.AddResponsibility(RecurrenceKind.Daily, D("2026-07-01"));
        await harness.Engine().TickAsync();
        (await harness.OccurrencesAsync(responsibility.Id)).Count.ShouldBe(1);

        // Pause through 10 July (the window ends at local midnight on the 11th).
        var tracked = await harness.Db.Responsibilities.FirstAsync(r => r.Id == responsibility.Id);
        tracked.PausedUntil = TenantTime.StartOfDay(D("2026-07-11"), harness.TimeZone);
        await harness.Db.SaveChangesAsync();

        harness.Clock.Set("2026-07-06T09:00:00Z");
        await harness.Engine().TickAsync();
        (await harness.OccurrencesAsync(responsibility.Id)).Count.ShouldBe(1); // nothing spawned while paused

        // Resume: the pause window's end stays on the row so the engine knows what was sanctioned.
        harness.Clock.Set("2026-07-12T09:00:00Z");
        await harness.Engine().TickAsync();

        var dates = (await harness.OccurrencesAsync(responsibility.Id))
            .Select(o => TenantTime.LocalDate(o.PeriodStart!.Value, harness.TimeZone))
            .ToArray();

        // 1 July existed before the pause; 2-10 July were skipped entirely; the ledger resumes on the 11th.
        dates.ShouldBe([D("2026-07-01"), D("2026-07-11"), D("2026-07-12")]);
        dates.ShouldNotContain(D("2026-07-05"));
    }

    [Fact]
    public async Task Occurrences_still_spawn_for_a_deactivated_owner()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-28T15:00:00Z");

        var responsibility = harness.AddResponsibility(RecurrenceKind.Daily, D("2026-07-27"));

        var owner = await harness.Db.Users.FirstAsync(u => u.Id == harness.Owner.Id);
        owner.Active = false;
        await harness.Db.SaveChangesAsync();

        await harness.Engine().TickAsync();

        var occurrences = await harness.OccurrencesAsync(responsibility.Id);
        occurrences.ShouldNotBeEmpty();
        occurrences.ShouldAllBe(o => o.OwnerUserId == harness.Owner.Id); // reassignment is v1.5
    }

    [Fact]
    public async Task A_deactivated_responsibility_stops_spawning_but_keeps_its_history()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-27T15:00:00Z");

        var responsibility = harness.AddResponsibility(RecurrenceKind.Daily, D("2026-07-27"));
        await harness.Engine().TickAsync();
        var before = (await harness.OccurrencesAsync(responsibility.Id)).Count;

        var tracked = await harness.Db.Responsibilities.FirstAsync(r => r.Id == responsibility.Id);
        tracked.Active = false;
        await harness.Db.SaveChangesAsync();

        harness.Clock.AdvanceDays(5);
        await harness.Engine().TickAsync();

        (await harness.OccurrencesAsync(responsibility.Id)).Count.ShouldBe(before);
    }

    [Fact]
    public async Task Monthly_day_31_produces_one_occurrence_per_month_clamped_to_the_month_end()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-05-01T09:00:00Z");

        var responsibility = harness.AddResponsibility(RecurrenceKind.MonthlyOnDay, D("2026-01-01"), dayOfMonth: 31);
        await harness.Engine().TickAsync();

        var dates = (await harness.OccurrencesAsync(responsibility.Id))
            .Select(o => TenantTime.LocalDate(o.PeriodStart!.Value, harness.TimeZone))
            .ToArray();

        dates.ShouldBe([D("2026-01-31"), D("2026-02-28"), D("2026-03-31"), D("2026-04-30")]);
    }

    [Fact]
    public async Task An_engine_created_occurrence_carries_a_Created_event_with_no_user()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-28T15:00:00Z");

        var responsibility = harness.AddResponsibility(RecurrenceKind.Daily, D("2026-07-28"));
        await harness.Engine().TickAsync();

        var occurrence = (await harness.OccurrencesAsync(responsibility.Id)).Single();

        var created = await harness.Db.WorkItemEvents.AsNoTracking()
            .SingleAsync(e => e.WorkItemId == occurrence.Id && e.EventType == WorkItemEventType.Created);

        created.UserId.ShouldBeNull();
        created.DataJson.ShouldNotBeNull();
        created.DataJson!.ShouldContain("engine");
    }

    [Fact]
    public async Task Occurrences_inherit_the_responsibility_entity_and_department_links()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-28T15:00:00Z");

        var entity = harness.AddEntity("Acme Distribution");
        var responsibility = harness.AddResponsibility(RecurrenceKind.Daily, D("2026-07-28"), entityId: entity.Id);

        await harness.Engine().TickAsync();

        var occurrence = (await harness.OccurrencesAsync(responsibility.Id)).Single();
        occurrence.EntityId.ShouldBe(entity.Id);
        occurrence.Title.ShouldBe(responsibility.Title);
    }
}
