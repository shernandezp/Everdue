using System.Net;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Filters arrive from hand-written URLs, bookmarks and report links, so the casing that a person
/// would naturally type has to work. Minimal APIs bind enums case-sensitively, which produced a
/// bodyless 400 for <c>?entityType=customer</c>; these lock in the parsing that replaced it.
/// </summary>
public class QueryFilterTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Enum_filters_are_case_insensitive(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        foreach (var url in new[]
                 {
                     "/api/v1/workitems?status=open",
                     "/api/v1/workitems?status=OPEN,missed",
                     "/api/v1/workitems?entityType=customer",
                     "/api/v1/workitems?holdReason=waitingcustomer",
                     "/api/v1/workitems?view=BOARD",
                     "/api/v1/entities?type=supplier",
                     "/api/v1/reports/entity-health?sort=daysSinceLastActivity&descending=true",
                     "/api/v1/reports/exceptions?entityType=equipment",
                     "/api/v1/reports/neglect?entityType=company&days=30",
                     "/api/v1/reports/blocked-by-entity?entityType=department",
                 })
        {
            (await client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.OK, url);
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_work_list_sorts_server_side(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        foreach (var title in new[] { "Bravo", "Alpha", "Charlie" })
        {
            await client.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
            {
                title,
                ownerUserId = ownerId,
                dueDate = app.Clock.UtcNow.AddDays(1),
            });
        }

        var ascending = await client.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems?sort=title");
        ascending.Items.Select(i => i.Title).ShouldBe(["Alpha", "Bravo", "Charlie"]);

        var descending = await client.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems?sort=TITLE&descending=true");
        descending.Items.Select(i => i.Title).ShouldBe(["Charlie", "Bravo", "Alpha"]);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_genuinely_invalid_filter_says_what_the_valid_values_are(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var response = await client.GetAsync("/api/v1/workitems?entityType=spaceship");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).ShouldBe("validation_failed");
        (await response.Content.ReadAsStringAsync()).ShouldContain(nameof(EntityType.Customer));
    }

    /// <summary>
    /// Search must mean the same thing on both providers: SQLite's LIKE ignores ASCII case and
    /// PostgreSQL's does not, so an install that moved from one to the other would quietly stop
    /// finding things.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Search_is_case_insensitive_on_both_providers(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await client.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "Acme Distribution",
            type = nameof(EntityType.Customer),
        });

        foreach (var term in new[] { "acme", "ACME", "AcMe", "distribution" })
        {
            var found = await client.GetJsonAsync<PagedResult<EntityDto>>($"/api/v1/entities?search={term}");
            found.TotalCount.ShouldBe(1, $"search term '{term}'");
        }

        var missing = await client.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities?search=globex");
        missing.TotalCount.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Task_search_matches_every_word_ignoring_case_accents_and_order(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var entity = await client.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "Ferretería El Progreso",
            type = nameof(EntityType.Customer),
        });

        async Task AddAsync(string title, string? description = null, Guid? entityId = null)
            => await client.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
            {
                title,
                description,
                entityId,
                ownerUserId = ownerId,
                dueDate = app.Clock.UtcNow.AddDays(1),
            });

        await AddAsync("Revisar la guía de RECEPCIÓN");
        await AddAsync("Llamar al proveedor", "Preguntar por la\nrecepción del pedido 50%");
        await AddAsync("Visita mensual", entityId: entity.Id);
        await AddAsync("Inventario");

        async Task<string[]> SearchAsync(string q)
            => (await client.GetJsonAsync<IReadOnlyList<WorkItemDto>>($"/api/v1/workitems/search?q={Uri.EscapeDataString(q)}"))
                .Select(w => w.Title).ToArray();

        (await SearchAsync("recepcion")).ShouldBe(["Revisar la guía de RECEPCIÓN", "Llamar al proveedor"]);
        (await SearchAsync("GUIA revisar")).ShouldBe(["Revisar la guía de RECEPCIÓN"]);
        (await SearchAsync("pedido prov")).ShouldBe(["Llamar al proveedor"]);
        (await SearchAsync("ferreteria")).ShouldBe(["Visita mensual"]);
        (await SearchAsync("50%")).ShouldBe(["Llamar al proveedor"]);
        (await SearchAsync("recepcion inventario")).ShouldBeEmpty();
        (await SearchAsync("  ")).ShouldBeEmpty();

        var listed = await client.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems?search=PEDIDO");
        listed.Items.Select(w => w.Title).ShouldBe(["Llamar al proveedor"]);

        var inventory = (await client.GetJsonAsync<IReadOnlyList<WorkItemDto>>("/api/v1/workitems/search?q=inventario")).Single();
        await client.PutJsonAsync<WorkItemDto>($"/api/v1/workitems/{inventory.Id}", new
        {
            title = "Conteo físico 한국어 😀",
            description = "Bodega norte",
            ownerUserId = ownerId,
        });

        (await SearchAsync("inventario")).ShouldBeEmpty();
        (await SearchAsync("fisico bodega")).Length.ShouldBe(1);
        (await SearchAsync("한국어")).Length.ShouldBe(1);

        await client.PutJsonAsync<EntityDto>($"/api/v1/entities/{entity.Id}", new
        {
            name = "Almacén Central",
            type = nameof(EntityType.Customer),
            active = true,
        });

        (await SearchAsync("ferreteria")).ShouldBeEmpty();
        (await SearchAsync("almacen")).ShouldBe(["Visita mensual"]);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_daily_responsibility_does_not_crowd_other_tasks_out_of_search(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        await app.SeedAsync((ledger, owner) =>
        {
            var daily = ledger.Responsibility("Daily stock check", owner);
            for (var day = 1; day <= 120; day++)
            {
                ledger.Occurrence(daily, ledger.Today.AddDays(-day), 1, WorkItemStatus.Missed);
            }
        });

        await client.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Check the fire extinguishers",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(-200),
        });

        var found = await client.GetJsonAsync<IReadOnlyList<WorkItemDto>>("/api/v1/workitems/search?q=check");

        found.Select(w => w.Title).ShouldBe(["Daily stock check", "Daily stock check", "Check the fire extinguishers"]);
    }

    /// <summary>A literal % must search for a percent sign, not match every row.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Wildcards_typed_by_the_user_are_treated_as_text(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await client.PostJsonAsync<EntityDto>("/api/v1/entities", new { name = "Acme", type = nameof(EntityType.Customer) });
        await client.PostJsonAsync<EntityDto>("/api/v1/entities", new { name = "100% Fresh", type = nameof(EntityType.Supplier) });

        var literal = await client.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities?search=%25");
        literal.TotalCount.ShouldBe(1);
        literal.Items.Single().Name.ShouldBe("100% Fresh");

        var underscore = await client.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities?search=_");
        underscore.TotalCount.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Paging_is_clamped_and_reported_back(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var clamped = await client.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems?pageSize=500&page=0");
        clamped.PageSize.ShouldBe(Paging.MaxPageSize);
        clamped.Page.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Picker_options_are_unpaged_and_active_only(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var created = new List<EntityDto>();
        for (var i = 0; i <= Paging.MaxPageSize + 1; i++)
        {
            created.Add(await client.PostJsonAsync<EntityDto>("/api/v1/entities", new { name = $"Client {i:D3}", type = nameof(EntityType.Customer) }));
        }

        var department = await client.PostJsonAsync<DepartmentDto>("/api/v1/departments", new { name = "Retired" });
        (await client.DeleteAsync($"/api/v1/entities/{created[0].Id}")).EnsureSuccessStatusCode();
        (await client.DeleteAsync($"/api/v1/departments/{department.Id}")).EnsureSuccessStatusCode();

        var active = await client.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities");
        var entities = await client.GetJsonAsync<List<EntityOptionDto>>("/api/v1/entities/options");
        entities.Count.ShouldBe(active.TotalCount);
        entities.Count.ShouldBeGreaterThan(Paging.MaxPageSize);
        entities.ShouldNotContain(e => e.Id == created[0].Id);

        var departments = await client.GetJsonAsync<List<DepartmentOptionDto>>("/api/v1/departments/options");
        departments.ShouldNotContain(d => d.Id == department.Id);
    }
}
