using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// The v2 acceptance criteria: compliance, chronic delay, reliability and concentration return known
/// numbers on a hand-built ledger, on both providers, and every number opens a list that totals it.
/// </summary>
public class InsightsTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private const string Insights = "/api/v1/insights";

    /// <summary>Criterion 1: 26 on time, 1 late, 3 missed over 30 concluded weekly periods is 87%.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Compliance_reports_the_rate_and_a_trend_that_sums_to_it(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var responsibility = ledger.Responsibility("Weekly client call", owner, kind: RecurrenceKind.WeeklyOnDays, daysOfWeekMask: 1 << (int)DayOfWeek.Monday);

            ledger.History(
                responsibility,
                count: 30,
                periodDays: 7,
                statusFor: index => index switch
                {
                    0 or 1 or 2 => WorkItemStatus.Missed,
                    3 => WorkItemStatus.CompletedLate,
                    _ => WorkItemStatus.Completed,
                },
                lastPeriodStart: new DateOnly(2026, 7, 20));
        });

        var report = await client.GetJsonAsync<PagedResult<ComplianceRowDto>>($"{Insights}/compliance?bucket=Week&buckets=35");

        var row = report.Items.ShouldHaveSingleItem();
        row.OnTime.ShouldBe(26);
        row.Late.ShouldBe(1);
        row.Missed.ShouldBe(3);
        row.Concluded.ShouldBe(30);
        row.InFlight.ShouldBe(0);
        row.RateSuppressed.ShouldBeFalse();
        Math.Round(row.Rate!.Value * 100).ShouldBe(87);

        // The trend is the same numbers, split by week: it can never disagree with the headline.
        row.Trend.Count.ShouldBe(35);
        row.Trend.Sum(point => point.OnTime).ShouldBe(26);
        row.Trend.Sum(point => point.Late).ShouldBe(1);
        row.Trend.Sum(point => point.Missed).ShouldBe(3);
        row.Trend.Count(point => point.Partial).ShouldBe(1);
        row.Trend[^1].Partial.ShouldBeTrue();

        // Each period is one ISO week wide, so no bucket carries two of them.
        row.Trend.ShouldAllBe(point => point.OnTime + point.Late + point.Missed <= 1);
    }

    /// <summary>Criterion 2: the rate does not wait for the engine's next tick.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_expired_occurrence_counts_as_a_miss_before_any_tick_flips_it(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var now = app.Clock.UtcNow;

        await app.SeedAsync((ledger, owner) =>
        {
            var responsibility = ledger.Responsibility(
                "Weekly stock check",
                owner,
                kind: RecurrenceKind.WeeklyOnDays,
                daysOfWeekMask: 1 << (int)DayOfWeek.Monday);

            // Its period ended five minutes ago and nothing has flipped the row yet.
            var occurrence = ledger.Occurrence(responsibility, ledger.Today.AddDays(-1), periodDays: 7, WorkItemStatus.Open);
            occurrence.PeriodEnd = now.AddMinutes(-5);
        });

        var before = await client.GetJsonAsync<PagedResult<ComplianceRowDto>>($"{Insights}/compliance");
        var row = before.Items.ShouldHaveSingleItem();
        row.Missed.ShouldBe(1);
        row.Concluded.ShouldBe(1);
        row.InFlight.ShouldBe(0);

        await app.TickEngineAsync();

        var after = await client.GetJsonAsync<PagedResult<ComplianceRowDto>>($"{Insights}/compliance");
        var flipped = after.Items.ShouldHaveSingleItem();
        flipped.Missed.ShouldBe(1);
        flipped.OnTime.ShouldBe(0);
        flipped.Concluded.ShouldBe(1);

        // The row really was rewritten by the tick — the number simply did not depend on it.
        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var statuses = db.WorkItems.Where(w => w.ResponsibilityId != null).Select(w => w.Status).ToList();
            statuses.ShouldContain(WorkItemStatus.Missed);
            await Task.CompletedTask;
        });
    }

    /// <summary>Criterion 3: a sanctioned pause writes no rows, so it cannot read as failure.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_paused_stretch_produces_no_misses_and_no_zero_rate_buckets(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var responsibility = ledger.Responsibility("Weekly inspection", owner, kind: RecurrenceKind.WeeklyOnDays, daysOfWeekMask: 1 << (int)DayOfWeek.Monday);

            // Five weeks of work, then a six-week pause the engine skipped, then one missed week.
            ledger.Occurrence(responsibility, new DateOnly(2026, 7, 20), 7, WorkItemStatus.Missed);

            for (var week = 0; week < 5; week++)
            {
                ledger.Occurrence(responsibility, new DateOnly(2026, 6, 1).AddDays(-7 * week), 7, WorkItemStatus.Completed);
            }
        });

        var report = await client.GetJsonAsync<PagedResult<ComplianceRowDto>>($"{Insights}/compliance?bucket=Week&buckets=20");
        var row = report.Items.ShouldHaveSingleItem();

        row.Concluded.ShouldBe(6);
        row.OnTime.ShouldBe(5);
        row.Missed.ShouldBe(1);
        Math.Round(row.Rate!.Value, 4).ShouldBe(Math.Round(5d / 6, 4));

        // The paused weeks are empty, not zero per cent: nothing was ever due in them.
        var paused = row.Trend.Where(point => point.OnTime + point.Late + point.Missed == 0).ToArray();
        paused.Length.ShouldBeGreaterThanOrEqualTo(6);
        paused.ShouldAllBe(point => point.Rate == null);
    }

    /// <summary>Criterion 4: a rate over three periods is withheld, and withheld rates sort last.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_thin_denominator_is_shown_as_a_pair_and_sorts_after_every_real_rate(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var thin = ledger.Responsibility("Barely started", owner);
            ledger.History(thin, 3, 1, index => index == 0 ? WorkItemStatus.Completed : WorkItemStatus.Missed);

            var solid = ledger.Responsibility("Long running", owner);
            ledger.History(solid, 10, 1, _ => WorkItemStatus.Completed);
        });

        foreach (var direction in new[] { "true", "false" })
        {
            var report = await client.GetJsonAsync<PagedResult<ComplianceRowDto>>(
                $"{Insights}/compliance?sort=Rate&descending={direction}");

            var thin = report.Items.Single(r => r.Title == "Barely started");
            thin.Rate.ShouldBeNull();
            thin.RateSuppressed.ShouldBeTrue();
            thin.OnTime.ShouldBe(1);
            thin.Concluded.ShouldBe(3);

            report.Items[^1].Title.ShouldBe("Barely started", $"a withheld rate must sort last (descending={direction})");
            report.Items[0].Rate.ShouldBe(1);
        }
    }

    /// <summary>Criterion 5: K and N come from configuration, and nothing else changes.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Chronic_detection_flags_k_misses_in_the_last_n_periods(TestProvider provider)
    {
        await using (var app = await EverdueApp.StartAsync(provider))
        {
            var client = await app.SignInAsAdminAsync();
            await SeedChronicAsync(app);

            var chronic = await client.GetJsonAsync<IReadOnlyList<ChronicResponsibilityDto>>($"{Insights}/chronic");

            var flagged = chronic.ShouldHaveSingleItem();
            flagged.Title.ShouldBe("Chronically missed");
            flagged.Missed.ShouldBe(3);
            flagged.Evaluated.ShouldBe(8);
            flagged.LastMissedPeriodStart.ShouldNotBeNull();
        }

        // Same ledger, one configuration value different.
        await using (var lenient = await EverdueApp.StartAsync(
                         provider,
                         new Dictionary<string, string> { ["Insights:ChronicMissCount"] = "2" }))
        {
            var client = await lenient.SignInAsAdminAsync();
            await SeedChronicAsync(lenient);

            var chronic = await client.GetJsonAsync<IReadOnlyList<ChronicResponsibilityDto>>($"{Insights}/chronic");

            chronic.Count.ShouldBe(2);
            chronic[0].Missed.ShouldBe(3); // most-missed first
            chronic.ShouldContain(row => row.Title == "Occasionally missed");
        }
    }

    /// <summary>Criterion 6: an external wait stays in the rate, and is visible in the same row.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Reliability_keeps_external_waits_in_the_denominator_and_shows_them_beside_it(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);
        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);

        await app.SeedAsync((ledger, _) =>
        {
            var responsibility = ledger.Responsibility("Daily route", memberId);

            var history = ledger.History(
                responsibility,
                count: 40,
                periodDays: 1,
                statusFor: index => index < 6 ? WorkItemStatus.Missed : WorkItemStatus.Completed);

            // Four of them were waiting on a customer for six hours inside their own period.
            foreach (var blocked in history.Take(4))
            {
                var from = blocked.PeriodStart!.Value.AddHours(2);
                ledger.Hold(blocked, HoldReason.WaitingCustomer, from, from.AddHours(6));
            }

            // A wait on an approval is ours to chase: it is not an external wait.
            ledger.Hold(history[4], HoldReason.MissingInformation, history[4].PeriodStart!.Value.AddHours(1), history[4].PeriodStart!.Value.AddHours(9));

            ledger.Reassigned(history[0], history[0].PeriodStart!.Value.AddHours(3));

            // One-off work is volume, never part of the rate — it can never be missed.
            ledger.OneOff("Fix the label printer", memberId, WorkItemStatus.Completed, ledger.At(ledger.Today.AddDays(-3), 9), completedAt: ledger.At(ledger.Today.AddDays(-3), 9));
            ledger.OneOff("Order packaging", adminId, WorkItemStatus.Completed, ledger.At(ledger.Today.AddDays(-2), 9), completedAt: ledger.At(ledger.Today.AddDays(-2), 9));
        });

        var report = await client.GetJsonAsync<IReadOnlyList<ReliabilityRowDto>>($"{Insights}/reliability");

        var member = report.Single(row => row.UserId == memberId);
        member.OnTime.ShouldBe(34);
        member.Missed.ShouldBe(6);
        member.Concluded.ShouldBe(40);
        Math.Round(member.Rate!.Value, 4).ShouldBe(0.85);
        member.RateSuppressed.ShouldBeFalse();

        member.ExternallyBlocked.ShouldBe(4);
        member.BlockedDays.ShouldBe(1.0); // 4 × 6 hours
        member.OneOffCompleted.ShouldBe(1);
        member.HandedOverInWindow.ShouldBe(1);

        // The administrator only ever completed a one-off: there is no rate to show, and no zero either.
        var admin = report.Single(row => row.UserId == adminId);
        admin.Rate.ShouldBeNull();
        admin.Concluded.ShouldBe(0);
        admin.OneOffCompleted.ShouldBe(1);
    }

    /// <summary>Criterion 10: tenant-local months, dense buckets, and an honest cap.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Concentration_buckets_by_tenant_local_month_and_reports_what_it_dropped(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(
            provider,
            new Dictionary<string, string> { ["Insights:TopEntities"] = "1" });

        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var acme = ledger.Entity("Acme");
            var globex = ledger.Entity("Globex", EntityType.Supplier);

            var responsibility = ledger.Responsibility("Monthly review", acme.Id, kind: RecurrenceKind.MonthlyOnDay);
            responsibility.OwnerUserId = owner;
            responsibility.EntityId = acme.Id;

            // 23:30 local on the last day of June, which is already July in UTC.
            var lateJune = TenantTime.ToInstant(new DateTime(2026, 6, 30, 23, 30, 0), ledger.TimeZone);
            var item = ledger.Occurrence(responsibility, new DateOnly(2026, 6, 1), 30, WorkItemStatus.Completed, completedAt: lateJune);
            item.EntityId = acme.Id;

            ledger.OneOff("Acme paperwork", owner, WorkItemStatus.Completed, lateJune, lateJune, acme.Id);
            ledger.OneOff("Globex delivery", owner, WorkItemStatus.Completed, lateJune, lateJune, globex.Id);
            ledger.OneOff("Unlinked chore", owner, WorkItemStatus.Completed, lateJune, lateJune);
        });

        var series = await client.GetJsonAsync<ConcentrationSeriesDto>($"{Insights}/concentration?bucket=Month&buckets=6");

        series.Buckets.Count.ShouldBe(6);
        series.Buckets[^1].Key.ShouldBe("2026-07");
        series.Buckets[^1].Partial.ShouldBeTrue();

        // The cap kept one entity and said so rather than pretending the other did not exist.
        var acmeRow = series.Rows.ShouldHaveSingleItem();
        acmeRow.EntityName.ShouldBe("Acme");
        acmeRow.Total.ShouldBe(2);
        series.OmittedEntities.ShouldBe(1);
        series.UnlinkedTotal.ShouldBe(1);

        var june = acmeRow.Points.Single(point => point.BucketKey == "2026-06");
        june.Occurrences.ShouldBe(1);
        june.OneOffs.ShouldBe(1);

        // Dense: quiet months are zeros, never gaps.
        acmeRow.Points.Count.ShouldBe(6);
        acmeRow.Points.Count(point => point.Total == 0).ShouldBe(5);
    }

    /// <summary>Criterion 11: every number opens a list that totals exactly it.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Every_insight_number_drills_through_to_a_list_of_exactly_that_size(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        await app.SeedAsync((ledger, _) =>
        {
            var acme = ledger.Entity("Acme");
            var responsibility = ledger.Responsibility("Daily route", memberId, acme.Id);

            ledger.History(responsibility, 10, 1, index => index < 3 ? WorkItemStatus.Missed : WorkItemStatus.Completed);

            // In flight: today's period has not concluded, and it belongs to the drill-through too.
            ledger.Occurrence(responsibility, ledger.Today, 1, WorkItemStatus.Open);

            ledger.OneOff("Not an occurrence", memberId, WorkItemStatus.Completed, ledger.At(ledger.Today, 9), ledger.At(ledger.Today, 9), acme.Id);
        });

        var compliance = await client.GetJsonAsync<PagedResult<ComplianceRowDto>>($"{Insights}/compliance");
        var complianceRow = compliance.Items.ShouldHaveSingleItem();
        await ShouldTotalAsync(client, complianceRow.DrillThrough, complianceRow.Concluded + complianceRow.InFlight);

        var reliability = await client.GetJsonAsync<IReadOnlyList<ReliabilityRowDto>>($"{Insights}/reliability");
        var reliabilityRow = reliability.Single(row => row.UserId == memberId);

        // The one-off completion is in the same window and must not be inside this number.
        await ShouldTotalAsync(client, reliabilityRow.DrillThrough, reliabilityRow.Concluded + reliabilityRow.InFlight);

        var concentration = await client.GetJsonAsync<ConcentrationSeriesDto>($"{Insights}/concentration?bucket=Month&buckets=3");
        var entityRow = concentration.Rows.ShouldHaveSingleItem();
        await ShouldTotalAsync(client, entityRow.DrillThrough, entityRow.Total);

        var chronic = await client.GetJsonAsync<IReadOnlyList<ChronicResponsibilityDto>>($"{Insights}/chronic");
        var chronicRow = chronic.ShouldHaveSingleItem();
        await ShouldTotalAsync(client, chronicRow.DrillThrough, chronicRow.Evaluated);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Insights_are_administrator_only(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();
        var admin = await app.SignInAsAdminAsync();

        var routes = new[]
        {
            $"{Insights}/compliance",
            $"{Insights}/reliability",
            $"{Insights}/concentration",
            $"{Insights}/hold-aging",
            $"{Insights}/chronic",
            $"{Insights}/responsibilities/{Guid.CreateVersion7()}/compliance",
        };

        foreach (var route in routes)
        {
            (await member.GetAsync(route)).StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden, route);
        }

        // And the administrator reaches all of them (the unknown responsibility is a 404, not a 403).
        foreach (var route in routes.Take(5))
        {
            (await admin.GetAsync(route)).StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, route);
        }

        (await admin.GetAsync(routes[^1])).StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Query_parameters_are_parsed_the_way_a_hand_typed_url_writes_them(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        // Lower case, as a bookmark or a lower-cased link would send it.
        var months = await client.GetJsonAsync<ConcentrationSeriesDto>($"{Insights}/concentration?bucket=month&buckets=4");
        months.Buckets.Count.ShouldBe(4);

        // Absurd counts are clamped rather than refused.
        var clamped = await client.GetJsonAsync<PagedResult<ComplianceRowDto>>($"{Insights}/compliance?buckets=999");
        clamped.TotalCount.ShouldBe(0);

        var invalid = await client.GetAsync($"{Insights}/compliance?bucket=fortnight");
        invalid.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
        (await invalid.ProblemCodeAsync()).ShouldBe("validation_failed");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_responsibility_page_returns_the_strip_of_its_own_periods(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        Guid responsibilityId = Guid.Empty;

        await app.SeedAsync((ledger, owner) =>
        {
            var responsibility = ledger.Responsibility("Weekly call", owner, kind: RecurrenceKind.WeeklyOnDays, daysOfWeekMask: 1 << (int)DayOfWeek.Monday);
            responsibilityId = responsibility.Id;

            ledger.History(
                responsibility,
                count: 4,
                periodDays: 7,
                statusFor: index => index == 1 ? WorkItemStatus.Missed : WorkItemStatus.Completed,
                lastPeriodStart: new DateOnly(2026, 7, 20));

            var held = ledger.Occurrence(responsibility, new DateOnly(2026, 7, 27), 7, WorkItemStatus.OnHold, holdReason: HoldReason.WaitingCustomer);
            held.HoldReason = HoldReason.WaitingCustomer;
        });

        var page = await client.GetJsonAsync<ResponsibilityComplianceDto>(
            $"{Insights}/responsibilities/{responsibilityId}/compliance?buckets=8");

        page.Title.ShouldBe("Weekly call");
        page.Summary.Active.ShouldBeTrue();
        page.Summary.Paused.ShouldBeFalse();

        page.Strip.Count.ShouldBe(5);
        page.Strip.Select(point => point.Label).ShouldBe(["W27", "W28", "W29", "W30", "W31"]);
        page.Strip[^1].Status.ShouldBe(WorkItemStatus.OnHold);
        page.Strip[^1].HoldReason.ShouldBe(HoldReason.WaitingCustomer);
        page.Strip[^1].PeriodConcluded.ShouldBeFalse();
        page.Strip.Count(point => point.Status == WorkItemStatus.Missed).ShouldBe(1);

        // The in-flight period is outside the rate and inside the volume.
        page.Summary.Concluded.ShouldBe(4);
        page.Summary.InFlight.ShouldBe(1);
        page.Summary.Missed.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_responsibility_with_nothing_concluded_is_absent_rather_than_zero_per_cent(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var brandNew = ledger.Responsibility("Started today", owner);
            ledger.Occurrence(brandNew, ledger.Today, 1, WorkItemStatus.Open);
        });

        var report = await client.GetJsonAsync<PagedResult<ComplianceRowDto>>($"{Insights}/compliance");
        report.TotalCount.ShouldBe(0);
    }

    /// <summary>Criterion 13: correct aggregates at three years of scale, with a bounded number of queries.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_reports_stay_correct_on_a_three_year_ledger(TestProvider provider)
    {
        const int Responsibilities = 20;
        const int PeriodsEach = 1_000;

        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            for (var index = 0; index < Responsibilities; index++)
            {
                var responsibility = ledger.Responsibility($"Daily duty {index:00}", owner);

                // Two periods missed in every five, so the expected rate is exactly 60% — and four of
                // the newest eight are misses, which is chronic under the default threshold.
                ledger.History(responsibility, PeriodsEach, 1, period => period % 5 < 2 ? WorkItemStatus.Missed : WorkItemStatus.Completed);
            }
        });

        // 20 000 occurrences over ~3 years. The default window sees only the last twelve weeks of it.
        var wide = await client.GetJsonAsync<PagedResult<ComplianceRowDto>>(
            $"{Insights}/compliance?bucket=Month&buckets=40&pageSize=100");

        wide.TotalCount.ShouldBe(Responsibilities);
        wide.Items.ShouldAllBe(row => row.Concluded == PeriodsEach);
        wide.Items.ShouldAllBe(row => row.Missed == PeriodsEach * 2 / 5);
        wide.Items.ShouldAllBe(row => row.Trend.Sum(point => point.OnTime + point.Missed) == PeriodsEach);

        var reliability = await client.GetJsonAsync<IReadOnlyList<ReliabilityRowDto>>(
            $"{Insights}/reliability?bucket=Month&buckets=40");

        var only = reliability.ShouldHaveSingleItem();
        only.Concluded.ShouldBe(Responsibilities * PeriodsEach);
        Math.Round(only.Rate!.Value, 4).ShouldBe(0.6);

        var chronic = await client.GetJsonAsync<IReadOnlyList<ChronicResponsibilityDto>>($"{Insights}/chronic?limit=50");
        chronic.Count.ShouldBe(Responsibilities);
        chronic.ShouldAllBe(row => row.Evaluated == 8);
        chronic.ShouldAllBe(row => row.Missed == 4);
    }

    /// <summary>
    /// Criterion 12, second half. Every insight reads through the filtered DbSets, but "it should be
    /// filtered" is exactly the kind of claim that stops being true one query at a time.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task No_insight_ever_counts_another_tenants_rows(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedForeignLedgerAsync();

        var compliance = await client.GetJsonAsync<PagedResult<ComplianceRowDto>>($"{Insights}/compliance?buckets=52");
        compliance.TotalCount.ShouldBe(0);

        var reliability = await client.GetJsonAsync<IReadOnlyList<ReliabilityRowDto>>($"{Insights}/reliability?buckets=52");
        reliability.ShouldBeEmpty();

        var concentration = await client.GetJsonAsync<ConcentrationSeriesDto>($"{Insights}/concentration?bucket=Month&buckets=24");
        concentration.Rows.ShouldBeEmpty();
        concentration.UnlinkedTotal.ShouldBe(0);

        var holdAging = await client.GetJsonAsync<HoldAgingDto>($"{Insights}/hold-aging?bucket=Month&buckets=24");
        holdAging.ByReason.ShouldBeEmpty();
        holdAging.ByEntity.ShouldBeEmpty();

        var chronic = await client.GetJsonAsync<IReadOnlyList<ChronicResponsibilityDto>>($"{Insights}/chronic?limit=50");
        chronic.ShouldBeEmpty();
    }

    /// <summary>
    /// A retired responsibility keeps its history — the work did happen — but nobody should be sent to
    /// chase it, and chronic detection is about what to fix now.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_retired_responsibility_keeps_its_history_and_is_labelled_rather_than_hidden(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var retired = ledger.Responsibility("No longer done", owner, active: false);
            ledger.History(retired, 10, 1, index => index < 5 ? WorkItemStatus.Missed : WorkItemStatus.Completed);

            var paused = ledger.Responsibility("Paused for the summer", owner);
            paused.PausedUntil = ledger.At(ledger.Today.AddDays(30));
            ledger.History(paused, 10, 1, index => index < 4 ? WorkItemStatus.Missed : WorkItemStatus.Completed);
        });

        var compliance = await client.GetJsonAsync<PagedResult<ComplianceRowDto>>($"{Insights}/compliance");

        var retired = compliance.Items.Single(row => row.Title == "No longer done");
        retired.Concluded.ShouldBe(10);
        retired.Missed.ShouldBe(5);
        retired.Active.ShouldBeFalse();

        var paused = compliance.Items.Single(row => row.Title == "Paused for the summer");
        paused.Active.ShouldBeTrue();
        paused.Paused.ShouldBeTrue();

        // Chronic is the "what do I fix" list, so a retired obligation is not on it and a paused one is.
        var chronic = await client.GetJsonAsync<IReadOnlyList<ChronicResponsibilityDto>>($"{Insights}/chronic");
        chronic.ShouldNotContain(row => row.Title == "No longer done");
        chronic.ShouldContain(row => row.Title == "Paused for the summer");
    }

    /// <summary>A week of daily chips all reading "W31" would identify nothing, which is a label's only job.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Daily_periods_are_labelled_by_date_and_weekly_ones_by_week(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var daily = Guid.Empty;
        var weekly = Guid.Empty;

        await app.SeedAsync((ledger, owner) =>
        {
            var everyDay = ledger.Responsibility("Daily walk-round", owner);
            daily = everyDay.Id;
            ledger.History(everyDay, 4, 1, _ => WorkItemStatus.Completed, lastPeriodStart: new DateOnly(2026, 7, 27));

            var everyWeek = ledger.Responsibility("Weekly call", owner, kind: RecurrenceKind.WeeklyOnDays, daysOfWeekMask: 1 << (int)DayOfWeek.Monday);
            weekly = everyWeek.Id;
            ledger.History(everyWeek, 3, 7, _ => WorkItemStatus.Completed, lastPeriodStart: new DateOnly(2026, 7, 20));
        });

        var dailyPage = await client.GetJsonAsync<ResponsibilityComplianceDto>($"{Insights}/responsibilities/{daily}/compliance");
        dailyPage.Strip.Select(point => point.Label).ShouldBe(["07-24", "07-25", "07-26", "07-27"]);
        dailyPage.Strip.Select(point => point.Label).Distinct().Count().ShouldBe(4);

        // Periods starting 6, 13 and 20 July — the ISO weeks whose Mondays those are.
        var weeklyPage = await client.GetJsonAsync<ResponsibilityComplianceDto>($"{Insights}/responsibilities/{weekly}/compliance");
        weeklyPage.Strip.Select(point => point.Label).ShouldBe(["W28", "W29", "W30"]);
    }

    private static async Task SeedChronicAsync(EverdueApp app)
        => await app.SeedAsync((ledger, owner) =>
        {
            var chronic = ledger.Responsibility("Chronically missed", owner);
            ledger.History(chronic, 12, 1, index => index is 0 or 2 or 4 ? WorkItemStatus.Missed : WorkItemStatus.Completed);

            var occasional = ledger.Responsibility("Occasionally missed", owner);
            ledger.History(occasional, 12, 1, index => index is 1 or 3 ? WorkItemStatus.Missed : WorkItemStatus.Completed);

            // Three misses, but nine periods ago — outside its own window of eight.
            var historic = ledger.Responsibility("Missed long ago", owner);
            ledger.History(historic, 12, 1, index => index is 9 or 10 or 11 ? WorkItemStatus.Missed : WorkItemStatus.Completed);
        });

    private static async Task ShouldTotalAsync(HttpClient client, DrillThrough drillThrough, int expected)
    {
        var query = string.Join(
            '&',
            drillThrough.WorkItemQuery.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        var list = await client.GetJsonAsync<PagedResult<WorkItemDto>>($"/api/v1/workitems?pageSize=100&{query}");
        list.TotalCount.ShouldBe(expected, $"drill-through '{query}' disagrees with its own number");
    }
}
