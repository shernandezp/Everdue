using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Demo;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Demo mode.
///
/// The point of the feature is that <strong>every screen has something on it</strong> — an empty install shows
/// nothing that makes Everdue different — so that is what is asserted, rather than trusting a screenshot.
/// </summary>
public class DemoModeTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static Dictionary<string, string> Seeded => new()
    {
        ["Demo:Seed"] = "true",
        ["Demo:Months"] = "6",
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Every_report_and_insight_screen_has_data(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, Seeded);
        var admin = await app.SignInAsAdminAsync();

        var entities = await admin.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities?pageSize=100");
        entities.TotalCount.ShouldBeGreaterThan(5);

        var responsibilities = await admin.GetJsonAsync<PagedResult<ResponsibilityDto>>("/api/v1/responsibilities?pageSize=100");
        responsibilities.TotalCount.ShouldBeGreaterThanOrEqualTo(10);

        // Every recurrence kind, so the recurrence UI has something of each to show.
        responsibilities.Items.Select(r => r.RecurrenceKind).Distinct().Count().ShouldBe(4);

        var exceptions = await admin.GetJsonAsync<ExceptionsReportDto>("/api/v1/reports/exceptions");
        exceptions.OnHold.Count.ShouldBeGreaterThan(0);

        var health = await admin.GetJsonAsync<PagedResult<EntityHealthRowDto>>("/api/v1/reports/entity-health?pageSize=100");
        health.Items.ShouldContain(row => row.Missed30 + row.Missed60 + row.Missed90 > 0);
        health.Items.ShouldContain(row => row.LastActivityAt != null);

        var blocked = await admin.GetJsonAsync<IReadOnlyList<BlockedByEntityGroupDto>>("/api/v1/reports/blocked-by-entity");
        blocked.ShouldNotBeEmpty();

        var compliance = await admin.GetJsonAsync<PagedResult<ComplianceRowDto>>("/api/v1/insights/compliance?pageSize=100");
        compliance.Items.ShouldContain(row => row.OnTime > 0);
        compliance.Items.ShouldContain(row => row.Missed > 0);

        // Something to fix, so the dashboard's chronic card is not empty on a fresh demo.
        var chronic = await admin.GetJsonAsync<IReadOnlyList<ChronicResponsibilityDto>>("/api/v1/insights/chronic");
        chronic.ShouldNotBeEmpty();

        var reliability = await admin.GetJsonAsync<IReadOnlyList<ReliabilityRowDto>>("/api/v1/insights/reliability");
        reliability.ShouldContain(row => row.Concluded > 0);

        var concentration = await admin.GetJsonAsync<ConcentrationSeriesDto>("/api/v1/insights/concentration");
        concentration.Rows.ShouldNotBeEmpty();

        // At least two distinct hold reasons, so "where does waiting time go" is a question with an answer.
        var holdAging = await admin.GetJsonAsync<HoldAgingDto>("/api/v1/insights/hold-aging");
        holdAging.ByReason.Count(row => row.Holds > 0).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Checklists_and_a_photo_rule_are_both_demonstrated(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, Seeded);
        var admin = await app.SignInAsAdminAsync();

        var responsibilities = await admin.GetJsonAsync<PagedResult<ResponsibilityDto>>("/api/v1/responsibilities?pageSize=100");

        responsibilities.Items.ShouldContain(r => r.ChecklistItemCount > 0);
        responsibilities.Items.ShouldContain(r => r.RequireChecklistToComplete);
        responsibilities.Items.ShouldContain(r => r.RequireAttachmentToComplete);

        // And the snapshot really was taken, not merely configured.
        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            (await db.ChecklistItems.CountAsync()).ShouldBeGreaterThan(0);
            (await db.ChecklistItems.CountAsync(item => item.CheckedAt != null)).ShouldBeGreaterThan(0);
        });
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Custom_fields_are_demonstrated_within_the_guardrail(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, Seeded);
        var admin = await app.SignInAsAdminAsync();

        var definitions = await admin.GetJsonAsync<IReadOnlyList<EntityFieldDefDto>>("/api/v1/entity-fields");

        // Two: one reference on a customer, one on a machine. Enough to show the feature, few enough not to suggest
        // an entity is a customer record.
        definitions.Count.ShouldBe(2);

        var entities = await admin.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities?pageSize=100");
        entities.Items.ShouldContain(entity => entity.CustomFields.Any(field => field.Value != null));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_ledger_is_real_rather_than_a_wall_of_misses(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, Seeded);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();

            var occurrences = await db.WorkItems.AsNoTracking()
                .Where(w => w.ResponsibilityId != null)
                .Select(w => w.Status)
                .ToListAsync();

            occurrences.Count.ShouldBeGreaterThan(200);

            var completed = occurrences.Count(status => status == WorkItemStatus.Completed);
            var missed = occurrences.Count(status => status.CountsAsMissed());

            // This is the whole reason the seeder writes the ledger itself rather than back-dating a StartDate and
            // letting the engine catch up: that produces nothing but misses, and a demo of 0% compliance teaches a
            // stranger the opposite of what the product does.
            completed.ShouldBeGreaterThan(missed);
            missed.ShouldBeGreaterThan(0);

            // One-off work too, so the concentration report can split it from recurring work.
            (await db.WorkItems.CountAsync(w => w.ResponsibilityId == null)).ShouldBeGreaterThan(5);

            // The events are what hold aging is reconstructed from; without them the demo's insight screens would
            // be a picture rather than a computation.
            (await db.WorkItemEvents.CountAsync()).ShouldBeGreaterThan(occurrences.Count);
        });
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Nothing_is_seeded_without_the_flag(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();

            (await db.Entities.CountAsync()).ShouldBe(0);
            (await db.WorkItems.CountAsync()).ShouldBe(0);
            (await db.Tenants.SingleAsync()).DemoMode.ShouldBeFalse();
        });
    }

    /// <summary>
    /// The invariant the whole feature rests on: <strong>demo data present implies the flag is set.</strong>
    ///
    /// <para>Seeded history is deliberately indistinguishable from real history — that is what makes the demo
    /// worth looking at — so the badge in the header is the only thing telling a team not to file real work
    /// into it. This asserts the <em>startup</em> path (<c>Demo:Seed</c>), which never goes through the
    /// demo-mode command that also sets the flag. Miss it and a demo install claims to be a real one.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_startup_seed_marks_the_tenant_as_demo(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, Seeded);

        await app.ScopedAsync(async services =>
            (await services.GetRequiredService<EverdueDbContext>().Tenants.SingleAsync()).DemoMode.ShouldBeTrue());

        // And it reaches the client through the session, which is where the badge is drawn from.
        var admin = await app.SignInAsAdminAsync();
        (await admin.GetJsonAsync<CurrentUserDto>("/api/v1/auth/me")).Tenant.DemoMode.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_seeder_refuses_a_database_that_already_holds_data(TestProvider provider)
    {
        // A real install with the flag set — as it would be if somebody copied the demo compose file and pointed it
        // at their own volume, which is exactly the mistake the guard exists for.
        await using var app = await EverdueApp.StartAsync(provider, Seeded);
        var admin = await app.SignInAsAdminAsync();

        var before = await app.ScopedAsync(async services =>
            await services.GetRequiredService<EverdueDbContext>().Entities.CountAsync());

        await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "A real customer",
            type = nameof(EntityType.Customer),
        });

        // Running the seeder again — what a restart does — must change nothing at all.
        await app.ScopedAsync(services => services.GetRequiredService<DemoDataSeeder>().SeedAsync());

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();

            (await db.Entities.CountAsync()).ShouldBe(before + 1);
            (await db.Entities.CountAsync(entity => entity.Name == "A real customer")).ShouldBe(1);
        });
    }
}
