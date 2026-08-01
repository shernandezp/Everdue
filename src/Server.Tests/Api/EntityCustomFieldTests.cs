using System.Net;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Custom fields on entities — the closest the product comes to the kind of ERP-style sprawl the design
/// deliberately avoids, so the tests are mostly about what they <em>cannot</em> do.
/// </summary>
public class EntityCustomFieldTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static Task<EntityFieldDefDto> ATextFieldAsync(HttpClient admin, string name = "Account manager")
        => admin.PostJsonAsync<EntityFieldDefDto>("/api/v1/entity-fields", new
        {
            entityType = nameof(EntityType.Customer),
            name,
            fieldType = nameof(EntityFieldType.Text),
        });

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_value_round_trips_through_the_entity(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var field = await ATextFieldAsync(admin);

        var created = await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "Acme Distribución",
            type = nameof(EntityType.Customer),
            customFields = new Dictionary<string, string> { [field.Id.ToString()] = "Ana Restrepo" },
        });

        created.CustomFields.Single().Value.ShouldBe("Ana Restrepo");

        // And it comes back on a read, resolved against the definition so the client needs no second lookup.
        var read = await admin.GetJsonAsync<EntityDto>($"/api/v1/entities/{created.Id}");
        read.CustomFields.Single().Name.ShouldBe("Account manager");
        read.CustomFields.Single().Value.ShouldBe("Ana Restrepo");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_cap_and_the_duplicate_name_are_both_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        for (var index = 0; index < 10; index++)
        {
            await ATextFieldAsync(admin, $"Field {index}");
        }

        var eleventh = await admin.PostJsonAsync("/api/v1/entity-fields", new
        {
            entityType = nameof(EntityType.Customer),
            name = "One too many",
            fieldType = nameof(EntityFieldType.Text),
        });

        eleventh.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Case-insensitively, because "Account Manager" and "account manager" are the same field to a person.
        var duplicate = await admin.PostJsonAsync("/api/v1/entity-fields", new
        {
            entityType = nameof(EntityType.Customer),
            name = "field 3",
            fieldType = nameof(EntityFieldType.Text),
        });

        duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Each_field_type_rejects_a_value_of_the_wrong_shape(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var number = await admin.PostJsonAsync<EntityFieldDefDto>("/api/v1/entity-fields", new
        {
            entityType = nameof(EntityType.Equipment),
            name = "Capacity",
            fieldType = nameof(EntityFieldType.Number),
        });

        var date = await admin.PostJsonAsync<EntityFieldDefDto>("/api/v1/entity-fields", new
        {
            entityType = nameof(EntityType.Equipment),
            name = "Purchased",
            fieldType = nameof(EntityFieldType.Date),
        });

        var choice = await admin.PostJsonAsync<EntityFieldDefDto>("/api/v1/entity-fields", new
        {
            entityType = nameof(EntityType.Equipment),
            name = "Condition",
            fieldType = nameof(EntityFieldType.Select),
            options = new[] { "Good", "Needs service" },
        });

        async Task<HttpStatusCode> TryAsync(Guid definitionId, string value)
        {
            var response = await admin.PostJsonAsync("/api/v1/entities", new
            {
                name = $"Machine {Guid.CreateVersion7()}",
                type = nameof(EntityType.Equipment),
                customFields = new Dictionary<string, string> { [definitionId.ToString()] = value },
            });

            return response.StatusCode;
        }

        (await TryAsync(number.Id, "abc")).ShouldBe(HttpStatusCode.BadRequest);
        (await TryAsync(date.Id, "31/02/2026")).ShouldBe(HttpStatusCode.BadRequest);
        (await TryAsync(choice.Id, "Broken")).ShouldBe(HttpStatusCode.BadRequest);

        // And the valid forms are accepted, including a choice matched case-insensitively.
        (await TryAsync(number.Id, "1200.5")).ShouldBe(HttpStatusCode.Created);
        (await TryAsync(date.Id, "2026-03-15")).ShouldBe(HttpStatusCode.Created);
        (await TryAsync(choice.Id, "needs service")).ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Text_longer_than_the_limit_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var field = await ATextFieldAsync(admin);

        var response = await admin.PostJsonAsync("/api/v1/entities", new
        {
            name = "Verbose Ltd",
            type = nameof(EntityType.Customer),
            customFields = new Dictionary<string, string> { [field.Id.ToString()] = new string('x', 201) },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Deleting_a_definition_leaves_the_entity_readable_and_drops_the_orphan(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var field = await ATextFieldAsync(admin);

        var entity = await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "Comercial Ríos",
            type = nameof(EntityType.Customer),
            customFields = new Dictionary<string, string> { [field.Id.ToString()] = "Carlos Méndez" },
        });

        await (await admin.DeleteAsync($"/api/v1/entity-fields/{field.Id}")).ShouldBeSuccessAsync();

        // No cleanup migration, no cascade, no tombstone: the value is simply not resolvable any more.
        var read = await admin.GetJsonAsync<EntityDto>($"/api/v1/entities/{entity.Id}");
        read.Name.ShouldBe("Comercial Ríos");
        read.CustomFields.ShouldBeEmpty();

        // And it is gone from the column the next time the entity is saved.
        await admin.PutJsonAsync<EntityDto>($"/api/v1/entities/{entity.Id}", new
        {
            name = "Comercial Ríos",
            type = nameof(EntityType.Customer),
            active = true,
            customFields = new Dictionary<string, string>(),
        });

        var again = await admin.GetJsonAsync<EntityDto>($"/api/v1/entities/{entity.Id}");
        again.CustomFields.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Omitting_the_section_entirely_leaves_stored_values_alone(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var field = await ATextFieldAsync(admin);

        var entity = await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "Hotel Miramar",
            type = nameof(EntityType.Customer),
            customFields = new Dictionary<string, string> { [field.Id.ToString()] = "Diana Ospina" },
        });

        // A client that knows nothing about custom fields must not be able to erase them by saving a name.
        await admin.PutJsonAsync<EntityDto>($"/api/v1/entities/{entity.Id}", new
        {
            name = "Hotel Miramar (renamed)",
            type = nameof(EntityType.Customer),
            active = true,
        });

        var read = await admin.GetJsonAsync<EntityDto>($"/api/v1/entities/{entity.Id}");
        read.CustomFields.Single().Value.ShouldBe("Diana Ospina");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_member_cannot_define_fields(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();

        var response = await member.PostJsonAsync("/api/v1/entity-fields", new
        {
            entityType = nameof(EntityType.Customer),
            name = "Credit limit",
            fieldType = nameof(EntityFieldType.Number),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task No_custom_field_reaches_a_report_or_an_insight(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var field = await ATextFieldAsync(admin, "Secret sauce");

        await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "Talleres Vega",
            type = nameof(EntityType.Customer),
            customFields = new Dictionary<string, string> { [field.Id.ToString()] = "MAGIC-VALUE" },
        });

        // Asserted against the response bodies rather than by inspection: "display-only" is a property of the
        // wire, and the only honest way to check it is to look at what leaves.
        foreach (var url in new[]
                 {
                     "/api/v1/reports/entity-health",
                     "/api/v1/reports/neglect",
                     "/api/v1/reports/blocked-by-entity",
                     "/api/v1/insights/compliance",
                     "/api/v1/insights/concentration",
                 })
        {
            var response = await admin.GetAsync(url);
            await response.ShouldBeSuccessAsync();

            var body = await response.Content.ReadAsStringAsync();
            body.ShouldNotContain("MAGIC-VALUE");
            body.ShouldNotContain("Secret sauce");
        }

        // Nor is there a filter parameter for one: an unknown query parameter is simply ignored, which is the
        // point — there is nothing to pass.
        var filtered = await admin.GetJsonAsync<PagedResult<WorkItemDto>>(
            $"/api/v1/workitems?customField={field.Id}&customValue=MAGIC-VALUE");

        filtered.ShouldNotBeNull();
    }
}
