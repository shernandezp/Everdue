using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Acceptance criterion 5: the five reports return known numbers on a fixed dataset, on both
/// providers, and every dashboard number drills through to a list totalling exactly that number.
/// </summary>
public class ReportCorrectnessTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_exception_dashboard_returns_the_expected_numbers(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        await ReportFixture.SeedAsync(app);

        var report = await client.GetJsonAsync<ExceptionsReportDto>("/api/v1/reports/exceptions");

        report.LocalDate.ShouldBe(new DateOnly(2026, 7, 28));
        report.DueToday.Count.ShouldBe(1);       // the cancelled item due today does not count
        report.CompletedToday.Count.ShouldBe(2); // one Completed, one CompletedLate
        report.Overdue.Count.ShouldBe(3);
        report.MissedInRange.Count.ShouldBe(1);  // still-missed only; the late completion is no longer actionable
        report.OnHold.Count.ShouldBe(2);

        report.OnHoldByReason.Count.ShouldBe(2);
        report.OnHoldByReason.Single(g => g.Reason == HoldReason.WaitingCustomer).Count.ShouldBe(1);
        report.OnHoldByReason.Single(g => g.Reason == HoldReason.WaitingSupplier).Count.ShouldBe(1);
        report.OnHoldByReason.ShouldAllBe(g => g.OldestHoldAt != null);
    }

    /// <summary>Every card must open a list of exactly the rows it counted — no more, no fewer.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Every_dashboard_number_drills_through_to_a_list_of_exactly_that_size(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        await ReportFixture.SeedAsync(app);

        var report = await client.GetJsonAsync<ExceptionsReportDto>("/api/v1/reports/exceptions");

        var metrics = new (string Name, MetricDto Metric)[]
        {
            ("dueToday", report.DueToday),
            ("completedToday", report.CompletedToday),
            ("overdue", report.Overdue),
            ("missedInRange", report.MissedInRange),
            ("onHold", report.OnHold),
        };

        foreach (var (name, metric) in metrics)
        {
            var list = await client.GetJsonAsync<PagedResult<WorkItemDto>>(Url(metric.DrillThrough));
            list.TotalCount.ShouldBe(metric.Count, $"drill-through for '{name}' disagrees with its own number");
        }

        foreach (var group in report.OnHoldByReason)
        {
            var list = await client.GetJsonAsync<PagedResult<WorkItemDto>>(Url(group.DrillThrough));
            list.TotalCount.ShouldBe(group.Count, $"drill-through for hold reason '{group.Reason}' disagrees");
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Entity_health_counts_and_sorts_correctly(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var seeded = await ReportFixture.SeedAsync(app);

        var report = await client.GetJsonAsync<PagedResult<EntityHealthRowDto>>("/api/v1/reports/entity-health");
        report.TotalCount.ShouldBe(3);

        var acme = report.Items.Single(r => r.EntityId == seeded.Acme);
        acme.Open.ShouldBe(2);
        acme.Overdue.ShouldBe(2);
        acme.Missed30.ShouldBe(2);   // one Missed and one CompletedLate: the miss is never erased
        acme.Missed90.ShouldBe(2);
        acme.OnHold.ShouldBe(1);
        acme.DaysSinceLastActivity.ShouldBe(0);

        var globex = report.Items.Single(r => r.EntityId == seeded.Globex);
        globex.Open.ShouldBe(0);
        globex.Overdue.ShouldBe(1);
        globex.OnHold.ShouldBe(1);
        globex.Missed30.ShouldBe(0);
        globex.DaysSinceLastActivity.ShouldBe(120);

        var initech = report.Items.Single(r => r.EntityId == seeded.Initech);
        initech.Open.ShouldBe(0);
        initech.LastActivityAt.ShouldBeNull();
        initech.DaysSinceLastActivity.ShouldBeNull();

        // Server-side sorting: never-touched entities are the most neglected, so they sort last ascending.
        var sorted = await client.GetJsonAsync<PagedResult<EntityHealthRowDto>>(
            "/api/v1/reports/entity-health?sort=DaysSinceLastActivity&descending=true");

        sorted.Items[0].EntityId.ShouldBe(seeded.Initech); // null == "infinitely long ago"
        sorted.Items[1].EntityId.ShouldBe(seeded.Globex);
        sorted.Items[2].EntityId.ShouldBe(seeded.Acme);

        var byOpen = await client.GetJsonAsync<PagedResult<EntityHealthRowDto>>("/api/v1/reports/entity-health?sort=Open&descending=true");
        byOpen.Items[0].EntityId.ShouldBe(seeded.Acme);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Entity_health_rows_drill_through_to_their_own_work(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var seeded = await ReportFixture.SeedAsync(app);

        var report = await client.GetJsonAsync<PagedResult<EntityHealthRowDto>>("/api/v1/reports/entity-health");
        var acme = report.Items.Single(r => r.EntityId == seeded.Acme);

        var list = await client.GetJsonAsync<PagedResult<WorkItemDto>>(Url(acme.DrillThrough));
        list.Items.ShouldAllBe(i => i.EntityId == seeded.Acme);
        list.TotalCount.ShouldBe(6); // every non-cancelled Acme row
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Neglect_lists_only_entities_that_have_ever_carried_work(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var seeded = await ReportFixture.SeedAsync(app);

        var report = await client.GetJsonAsync<IReadOnlyList<NeglectRowDto>>("/api/v1/reports/neglect?days=90");

        report.Count.ShouldBe(1);
        report[0].EntityId.ShouldBe(seeded.Globex);
        report[0].DaysSinceLastActivity.ShouldBe(120);

        // Initech has never carried work: unused is not the same thing as neglected.
        report.ShouldNotContain(r => r.EntityId == seeded.Initech);
        report.ShouldNotContain(r => r.EntityId == seeded.Acme);

        // A shorter window pulls Acme in too.
        var aggressive = await client.GetJsonAsync<IReadOnlyList<NeglectRowDto>>("/api/v1/reports/neglect?days=1");
        aggressive.Count.ShouldBe(1); // Acme's last activity is today, so it still does not qualify
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Blocked_by_entity_groups_by_entity_and_reason(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var seeded = await ReportFixture.SeedAsync(app);

        var report = await client.GetJsonAsync<IReadOnlyList<BlockedByEntityGroupDto>>("/api/v1/reports/blocked-by-entity");

        report.Count.ShouldBe(2);

        var acme = report.Single(g => g.EntityId == seeded.Acme);
        acme.Total.ShouldBe(1);
        acme.Reasons.Single().Reason.ShouldBe(HoldReason.WaitingCustomer);
        acme.OldestHoldAt.ShouldNotBeNull();

        var globex = report.Single(g => g.EntityId == seeded.Globex);
        globex.Reasons.Single().Reason.ShouldBe(HoldReason.WaitingSupplier);

        foreach (var group in report)
        {
            var list = await client.GetJsonAsync<PagedResult<WorkItemDto>>(Url(group.DrillThrough));
            list.TotalCount.ShouldBe(group.Total);
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_entity_timeline_interleaves_occurrences_and_one_off_work(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var seeded = await ReportFixture.SeedAsync(app);

        var timeline = await client.GetJsonAsync<EntityTimelineDto>($"/api/v1/reports/entities/{seeded.Acme}/timeline");

        timeline.EntityId.ShouldBe(seeded.Acme);
        timeline.Items.Count.ShouldBe(6); // cancelled work is excluded from reports
        timeline.LastActivityAt.ShouldNotBeNull();

        // Newest first, and the sort key is the period start where there is one.
        timeline.Items.Zip(timeline.Items.Skip(1)).ShouldAllBe(pair => pair.First.SortDate >= pair.Second.SortDate);
        timeline.Items.ShouldContain(i => i.Status == WorkItemStatus.CompletedLate);
        timeline.Items.ShouldContain(i => i.Status == WorkItemStatus.Missed);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Report_filters_narrow_every_number_consistently(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var seeded = await ReportFixture.SeedAsync(app);

        var suppliersOnly = await client.GetJsonAsync<ExceptionsReportDto>(
            $"/api/v1/reports/exceptions?entityType={nameof(EntityType.Supplier)}");

        suppliersOnly.OnHold.Count.ShouldBe(1);
        suppliersOnly.OnHoldByReason.Single().Reason.ShouldBe(HoldReason.WaitingSupplier);

        var list = await client.GetJsonAsync<PagedResult<WorkItemDto>>(Url(suppliersOnly.OnHold.DrillThrough));
        list.TotalCount.ShouldBe(1);
        list.Items.Single().EntityId.ShouldBe(seeded.Globex);
    }

    private static string Url(DrillThrough drillThrough)
    {
        var query = string.Join(
            '&',
            drillThrough.WorkItemQuery.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return $"/api/v1/workitems?pageSize=100&{query}";
    }
}

/// <summary>
/// A hand-built dataset with numbers worked out on paper. Rows are inserted through the DbContext so
/// the fixture states exactly what it means, instead of depending on the API and the engine to
/// reproduce a particular history.
/// </summary>
internal static class ReportFixture
{
    internal sealed record Seeded(Guid Acme, Guid Globex, Guid Initech, Guid OwnerId);

    public static async Task<Seeded> SeedAsync(EverdueApp app)
        => await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var now = app.Clock.UtcNow; // 2026-07-28T15:00Z == 10:00 in Bogota
            var timeZone = TimeZoneLookup.Resolve("America/Bogota");
            var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

            var acme = NewEntity("Acme Distribution", EntityType.Customer);
            var globex = NewEntity("Globex Supplies", EntityType.Supplier);
            var initech = NewEntity("Initech (never used)", EntityType.Customer);
            db.Entities.AddRange(acme, globex, initech);

            DateTimeOffset Due(string localDate) => TenantTime.EndOfDay(DateOnly.Parse(localDate), timeZone);

            var items = new List<WorkItem>
            {
                // Acme
                Item(acme.Id, "Due today", WorkItemStatus.Open, Due("2026-07-28")),
                Item(acme.Id, "Overdue and open", WorkItemStatus.Open, Due("2026-07-20")),
                Hold(Item(acme.Id, "Blocked on the customer", WorkItemStatus.OnHold, Due("2026-07-25")), HoldReason.WaitingCustomer),
                Occurrence(acme.Id, "Missed weekly follow-up", WorkItemStatus.Missed, Due("2026-07-10"), timeZone, "2026-07-10", "2026-07-17"),
                Done(Occurrence(acme.Id, "Late weekly follow-up", WorkItemStatus.CompletedLate, Due("2026-07-12"), timeZone, "2026-07-12", "2026-07-19"), now, ownerId),
                Done(Item(acme.Id, "Completed yesterday's task", WorkItemStatus.Completed, Due("2026-07-27")), now, ownerId),
                Item(acme.Id, "No longer applies", WorkItemStatus.Cancelled, Due("2026-07-28")),

                // Globex
                Hold(Item(globex.Id, "Blocked on the supplier", WorkItemStatus.OnHold, Due("2026-07-26")), HoldReason.WaitingSupplier),
                Done(Item(globex.Id, "Ancient completed work", WorkItemStatus.Completed, Due("2026-03-30")), now.AddDays(-120), ownerId),
            };

            foreach (var item in items)
            {
                item.OwnerUserId = ownerId;
                item.CreatedAt = now.AddDays(-40);
            }

            db.WorkItems.AddRange(items);
            await db.SaveChangesAsync();

            // Hold events give "oldest hold" something to read; the engine and the API both write these.
            foreach (var held in items.Where(i => i.Status == WorkItemStatus.OnHold))
            {
                db.WorkItemEvents.Add(new WorkItemEvent
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = held.TenantId,
                    WorkItemId = held.Id,
                    UserId = ownerId,
                    Timestamp = now.AddDays(-3),
                    EventType = WorkItemEventType.StatusChanged,
                    FromStatus = WorkItemStatus.Open,
                    ToStatus = WorkItemStatus.OnHold,
                });
            }

            await db.SaveChangesAsync();

            return new Seeded(acme.Id, globex.Id, initech.Id, ownerId);
        });

    private static Entity NewEntity(string name, EntityType type)
        => new() { Id = Guid.CreateVersion7(), Name = name, Type = type, Active = true };

    private static WorkItem Item(Guid entityId, string title, WorkItemStatus status, DateTimeOffset due)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            EntityId = entityId,
            Status = status,
            DueDate = due,
        };

    private static WorkItem Occurrence(
        Guid entityId,
        string title,
        WorkItemStatus status,
        DateTimeOffset due,
        TimeZoneInfo timeZone,
        string periodStart,
        string periodEnd)
    {
        var item = Item(entityId, title, status, due);
        item.ResponsibilityId = null; // no responsibility row needed; the period columns are what reports read
        item.PeriodStart = TenantTime.StartOfDay(DateOnly.Parse(periodStart), timeZone);
        item.PeriodEnd = TenantTime.StartOfDay(DateOnly.Parse(periodEnd), timeZone);
        return item;
    }

    private static WorkItem Hold(WorkItem item, HoldReason reason)
    {
        item.HoldReason = reason;
        return item;
    }

    private static WorkItem Done(WorkItem item, DateTimeOffset at, Guid userId)
    {
        item.CompletedAt = at;
        item.CompletedByUserId = userId;
        return item;
    }
}
