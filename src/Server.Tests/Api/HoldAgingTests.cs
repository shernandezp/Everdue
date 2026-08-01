using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Hold aging is rebuilt from the event log, and each of the ways that reconstruction can go wrong gets
/// a test named after it. These are the tests that make the report trustworthy on history nobody
/// recorded for reporting.
/// </summary>
public class HoldAgingTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private const string HoldAging = "/api/v1/insights/hold-aging";

    /// <summary>Criterion 7: sequential holds are separate intervals, each with its own reason.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Sequential_holds_with_different_reasons_never_pool_into_one(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var item = ledger.OneOff("Chase the paperwork", owner, WorkItemStatus.Open, ledger.At(ledger.Today, 12));

            var day = ledger.Today.AddDays(-20);
            ledger.Hold(item, HoldReason.WaitingCustomer, ledger.At(day), ledger.At(day.AddDays(3)));
            ledger.Hold(item, HoldReason.WaitingSupplier, ledger.At(day.AddDays(5)), ledger.At(day.AddDays(7)));
        });

        var report = await client.GetJsonAsync<HoldAgingDto>(HoldAging);

        report.ByReason.Count.ShouldBe(2);

        var customer = report.ByReason.Single(row => row.Reason == HoldReason.WaitingCustomer);
        customer.TotalWaitDays.ShouldBe(3.0);
        customer.Holds.ShouldBe(1);
        customer.Items.ShouldBe(1);
        customer.StillOnHold.ShouldBe(0);
        customer.CurrentDrillThrough.ShouldBeNull("a hold that has ended leaves nothing to link to");

        var supplier = report.ByReason.Single(row => row.Reason == HoldReason.WaitingSupplier);
        supplier.TotalWaitDays.ShouldBe(2.0);
        supplier.LongestWaitDays.ShouldBe(2.0);
    }

    /// <summary>Criterion 8: a reschedule copies the status into both ends of its event. It is not a hold.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Rescheduling_a_held_item_neither_opens_nor_closes_a_hold(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var plain = ledger.OneOff("Untouched", owner, WorkItemStatus.Open, ledger.At(ledger.Today, 12));
            var day = ledger.Today.AddDays(-10);
            ledger.Hold(plain, HoldReason.WaitingApproval, ledger.At(day), ledger.At(day.AddDays(4)));

            var moved = ledger.OneOff("Rescheduled while held", owner, WorkItemStatus.Open, ledger.At(ledger.Today, 12));
            ledger.Hold(moved, HoldReason.WaitingApproval, ledger.At(day), ledger.At(day.AddDays(4)));

            // Two reschedules inside the hold: if the event type were ignored, this would read as the
            // hold ending and restarting twice.
            ledger.Rescheduled(moved, ledger.At(day.AddDays(1), 9));
            ledger.Rescheduled(moved, ledger.At(day.AddDays(2), 9));
        });

        var report = await client.GetJsonAsync<HoldAgingDto>(HoldAging);

        var approval = report.ByReason.ShouldHaveSingleItem();
        approval.Reason.ShouldBe(HoldReason.WaitingApproval);
        approval.Holds.ShouldBe(2);
        approval.Items.ShouldBe(2);
        approval.TotalWaitDays.ShouldBe(8.0);
        approval.LongestWaitDays.ShouldBe(4.0);
        approval.AverageWaitDays.ShouldBe(4.0);
    }

    /// <summary>Criterion 9: an old, still-open hold contributes only its in-window part, and can be opened.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_open_hold_from_before_the_window_contributes_only_the_days_inside_it(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var now = app.Clock.UtcNow;

        await app.SeedAsync((ledger, owner) =>
        {
            var item = ledger.OneOff("Waiting since forever", owner, WorkItemStatus.OnHold, ledger.At(ledger.Today, 12));
            item.HoldReason = HoldReason.WaitingCustomer;

            // Opened long before the reporting window and never released.
            ledger.Hold(item, HoldReason.WaitingCustomer, ledger.At(ledger.Today.AddDays(-200)));
        });

        var report = await client.GetJsonAsync<HoldAgingDto>($"{HoldAging}?bucket=Week&buckets=12");

        var row = report.ByReason.ShouldHaveSingleItem();
        row.StillOnHold.ShouldBe(1);
        report.To.ShouldBe(now);

        // Exactly the window, not the two hundred days behind it.
        var expected = Math.Round((report.To - report.From).TotalDays, 1, MidpointRounding.AwayFromZero);
        row.TotalWaitDays.ShouldBe(expected);
        row.TotalWaitDays.ShouldBeLessThan(200);

        // The one number that is still expressible as a work-item filter is the open holds.
        row.CurrentDrillThrough.ShouldNotBeNull();
        var query = string.Join('&', row.CurrentDrillThrough!.WorkItemQuery.Select(p => $"{p.Key}={p.Value}"));
        var list = await client.GetJsonAsync<PagedResult<WorkItemDto>>($"/api/v1/workitems?pageSize=100&{query}");
        list.TotalCount.ShouldBe(row.StillOnHold);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_hold_the_engine_ended_by_recording_a_miss_closes_at_the_miss(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var responsibility = ledger.Responsibility("Daily check", owner);
            var occurrence = ledger.Occurrence(responsibility, ledger.Today.AddDays(-5), 1, WorkItemStatus.Missed);

            // The engine flipping a held occurrence to Missed does not clear the reason column, so this
            // row still says "waiting on the supplier" — and the interval must still have ended.
            occurrence.HoldReason = HoldReason.WaitingSupplier;

            ledger.Hold(
                occurrence,
                HoldReason.WaitingSupplier,
                ledger.At(ledger.Today.AddDays(-5), 8),
                until: ledger.At(ledger.Today.AddDays(-4), 8),
                exitStatus: WorkItemStatus.Missed);
        });

        var report = await client.GetJsonAsync<HoldAgingDto>(HoldAging);

        var row = report.ByReason.ShouldHaveSingleItem();
        row.StillOnHold.ShouldBe(0, "the miss ended the hold");
        row.TotalWaitDays.ShouldBe(1.0);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_hold_whose_payload_cannot_be_read_is_counted_as_other_rather_than_lost(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var day = ledger.Today.AddDays(-3);

            var missing = ledger.OneOff("No payload", owner, WorkItemStatus.Open, ledger.At(ledger.Today, 12));
            ledger.Hold(missing, HoldReason.WaitingCustomer, ledger.At(day), ledger.At(day.AddDays(1)), entryPayload: string.Empty);

            var broken = ledger.OneOff("Broken payload", owner, WorkItemStatus.Open, ledger.At(ledger.Today, 12));
            ledger.Hold(broken, HoldReason.WaitingCustomer, ledger.At(day), ledger.At(day.AddDays(1)), entryPayload: "{not json");

            var unknown = ledger.OneOff("Unknown reason", owner, WorkItemStatus.Open, ledger.At(ledger.Today, 12));
            ledger.Hold(unknown, HoldReason.WaitingCustomer, ledger.At(day), ledger.At(day.AddDays(1)), entryPayload: "{\"reason\":\"WaitingForGodot\"}");
        });

        var report = await client.GetJsonAsync<HoldAgingDto>(HoldAging);

        var row = report.ByReason.ShouldHaveSingleItem();
        row.Reason.ShouldBe(HoldReason.Other);
        row.Holds.ShouldBe(3);
        row.TotalWaitDays.ShouldBe(3.0);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Wait_time_is_grouped_by_entity_and_says_what_the_cap_dropped(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(
            provider,
            new Dictionary<string, string> { ["Insights:TopEntities"] = "1" });

        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var acme = ledger.Entity("Acme");
            var globex = ledger.Entity("Globex", EntityType.Supplier);
            var day = ledger.Today.AddDays(-15);

            var slow = ledger.OneOff("Acme approval", owner, WorkItemStatus.Open, ledger.At(ledger.Today, 12), entityId: acme.Id);
            ledger.Hold(slow, HoldReason.WaitingCustomer, ledger.At(day), ledger.At(day.AddDays(6)));

            var quick = ledger.OneOff("Globex part", owner, WorkItemStatus.Open, ledger.At(ledger.Today, 12), entityId: globex.Id);
            ledger.Hold(quick, HoldReason.WaitingSupplier, ledger.At(day), ledger.At(day.AddDays(2)));

            var unlinked = ledger.OneOff("Nobody's job", owner, WorkItemStatus.Open, ledger.At(ledger.Today, 12));
            ledger.Hold(unlinked, HoldReason.Other, ledger.At(day), ledger.At(day.AddDays(1)));
        });

        var report = await client.GetJsonAsync<HoldAgingDto>(HoldAging);

        var top = report.ByEntity.ShouldHaveSingleItem();
        top.EntityName.ShouldBe("Acme");
        top.TotalWaitDays.ShouldBe(6.0);
        report.OmittedEntities.ShouldBe(2, "the supplier and the unlinked work were dropped, and it says so");

        // Filtering by entity narrows every number consistently, unlinked work included.
        var suppliersOnly = await client.GetJsonAsync<HoldAgingDto>($"{HoldAging}?entityType=Supplier");
        suppliersOnly.ByReason.ShouldHaveSingleItem().Reason.ShouldBe(HoldReason.WaitingSupplier);
        suppliersOnly.ByEntity.ShouldHaveSingleItem().EntityName.ShouldBe("Globex");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_window_that_ended_in_the_past_offers_no_link_to_the_present(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await app.SeedAsync((ledger, owner) =>
        {
            var item = ledger.OneOff("Still waiting", owner, WorkItemStatus.OnHold, ledger.At(ledger.Today, 12));
            item.HoldReason = HoldReason.WaitingCustomer;
            ledger.Hold(item, HoldReason.WaitingCustomer, ledger.At(ledger.Today.AddDays(-90)));
        });

        var historic = await client.GetJsonAsync<HoldAgingDto>(
            $"{HoldAging}?from=2026-05-01T00:00:00Z&to=2026-05-31T00:00:00Z&bucket=Month");

        var row = historic.ByReason.ShouldHaveSingleItem();
        row.StillOnHold.ShouldBe(1, "it was on hold throughout that month");
        row.CurrentDrillThrough.ShouldBeNull("what is on hold today is a different question");
        historic.To.ShouldBeLessThan(app.Clock.UtcNow);
    }
}
