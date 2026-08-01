using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Imports;
using Everdue.Server.Domain;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// CSV import: the on-ramp off a spreadsheet.
///
/// Two behaviours matter most. It must read what Excel actually writes — including a semicolon-separated,
/// BOM-prefixed file from a Spanish machine — and it must never overwrite: an import creates or skips.
/// </summary>
public class ImportTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    /// <summary>A file as a browser would post it, encoded the way the test asks for.</summary>
    private static MultipartFormDataContent Csv(string content, bool bom = false, char delimiter = ',', string? mapping = null)
    {
        var text = delimiter == ';' ? content.Replace(',', ';') : content;
        var bytes = new UTF8Encoding(bom).GetBytes(text);

        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        var form = new MultipartFormDataContent { { file, "file", "import.csv" } };

        if (mapping is not null)
        {
            form.Add(new StringContent(mapping), "mapping");
        }

        return form;
    }

    private static string Mapping(params (string Field, string Header)[] pairs)
        => JsonSerializer.Serialize(pairs.ToDictionary(pair => pair.Field, pair => pair.Header));

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_preview_writes_nothing_and_suggests_a_mapping(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var response = await admin.PostAsync(
            "/api/v1/imports/entities/preview",
            Csv("Name,Type\nAcme Distribución,Customer\nSuministros Andinos,Supplier\n"));

        await response.ShouldBeSuccessAsync();

        var preview = await response.Content.ReadFromJsonAsync<ImportPreviewDto>(EverdueApp.Json);
        preview.ShouldNotBeNull();

        preview!.TotalRows.ShouldBe(2);
        preview.Delimiter.ShouldBe(',');
        preview.SuggestedMapping[ImportFields.Name].ShouldBe("Name");
        preview.SuggestedMapping[ImportFields.Type].ShouldBe("Type");
        preview.Rows.ShouldAllBe(row => row.Error == null);

        // Nothing was written: a preview is a rehearsal.
        var entities = await admin.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities");
        entities.TotalCount.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_spanish_excel_file_imports(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        // Semicolons, a BOM, accented names and Spanish header words — what a colleague's export looks like.
        var content = "Nombre,Tipo\nFerretería El Progreso,Customer\nEmpaques del Norte,Supplier\n";

        var preview = await admin.PostAsync("/api/v1/imports/entities/preview", Csv(content, bom: true, delimiter: ';'));
        await preview.ShouldBeSuccessAsync();

        var parsed = (await preview.Content.ReadFromJsonAsync<ImportPreviewDto>(EverdueApp.Json))!;
        parsed.Delimiter.ShouldBe(';');
        parsed.SuggestedMapping[ImportFields.Name].ShouldBe("Nombre");

        var commit = await admin.PostAsync(
            "/api/v1/imports/entities/commit",
            Csv(content, bom: true, delimiter: ';', mapping: Mapping((ImportFields.Name, "Nombre"), (ImportFields.Type, "Tipo"))));

        await commit.ShouldBeSuccessAsync();

        var result = (await commit.Content.ReadFromJsonAsync<ImportResultDto>(EverdueApp.Json))!;
        result.Created.ShouldBe(2);
        result.Failed.ShouldBe(0);

        var entities = await admin.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities");
        entities.Items.Select(entity => entity.Name).ShouldContain("Ferretería El Progreso");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Valid_rows_are_created_and_invalid_ones_are_reported_with_their_row_number(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var content = string.Join('\n',
            "Name,Type",
            "Good One,Customer",
            ",Customer",                  // row 3: no name
            "Bad Type,Wombat",            // row 4: not an entity type
            "Good Two,Supplier",
            string.Empty);

        var commit = await admin.PostAsync(
            "/api/v1/imports/entities/commit",
            Csv(content, mapping: Mapping((ImportFields.Name, "Name"), (ImportFields.Type, "Type"))));

        await commit.ShouldBeSuccessAsync();

        var result = (await commit.Content.ReadFromJsonAsync<ImportResultDto>(EverdueApp.Json))!;

        // A single bad date must never reject the rows that were fine.
        result.Created.ShouldBe(2);
        result.Failed.ShouldBe(2);
        result.Failures.Select(failure => failure.RowNumber).ShouldBe([3, 4]);
        result.Failures[1].Message.ShouldContain("Wombat");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_duplicate_is_skipped_not_updated(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new { name = "Acme Ltd", type = nameof(EntityType.Customer) });

        // Same entity, different case, and a different `Active` — none of which may overwrite anything.
        var commit = await admin.PostAsync(
            "/api/v1/imports/entities/commit",
            Csv(
                "Name,Type,Active\nacme ltd,Customer,false\nNew Client,Customer,true\n",
                mapping: Mapping((ImportFields.Name, "Name"), (ImportFields.Type, "Type"), (ImportFields.Active, "Active"))));

        var result = (await commit.Content.ReadFromJsonAsync<ImportResultDto>(EverdueApp.Json))!;

        result.Created.ShouldBe(1);
        result.Skipped.ShouldBe(1);

        var entities = await admin.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities?includeInactive=true");
        entities.Items.Single(entity => entity.Name == "Acme Ltd").Active.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_custom_field_column_maps_and_validates_through_the_same_path(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var field = await admin.PostJsonAsync<EntityFieldDefDto>("/api/v1/entity-fields", new
        {
            entityType = nameof(EntityType.Customer),
            name = "Account manager",
            fieldType = nameof(EntityFieldType.Text),
        });

        var content = "Name,Type,Manager\nHotel Miramar,Customer,Diana Ospina\n";

        var commit = await admin.PostAsync(
            "/api/v1/imports/entities/commit",
            Csv(content, mapping: Mapping(
                (ImportFields.Name, "Name"),
                (ImportFields.Type, "Type"),
                (ImportFields.Custom(field.Id), "Manager"))));

        await commit.ShouldBeSuccessAsync();

        var entities = await admin.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities");
        entities.Items.Single().CustomFields.Single().Value.ShouldBe("Diana Ospina");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Work_items_import_as_open_one_offs_and_unknown_references_fail_that_row_only(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new { name = "Acme Ltd", type = nameof(EntityType.Customer) });

        var content = string.Join('\n',
            "Title,Due,Client",
            "Send the quotation,2026-08-10,Acme Ltd",
            "Chase the invoice,2026-08-11,Nobody Ltd",     // row 3: unknown entity
            "Book the inspection,not-a-date,Acme Ltd",     // row 4: unparseable date
            "Return the pallet jack,2026-08-12,",
            string.Empty);

        var commit = await admin.PostAsync(
            "/api/v1/imports/workitems/commit",
            Csv(content, mapping: Mapping(
                (ImportFields.Title, "Title"),
                (ImportFields.DueDate, "Due"),
                (ImportFields.Entity, "Client"))));

        await commit.ShouldBeSuccessAsync();

        var result = (await commit.Content.ReadFromJsonAsync<ImportResultDto>(EverdueApp.Json))!;
        result.Created.ShouldBe(2);
        result.Failures.Select(failure => failure.RowNumber).ShouldBe([3, 4]);

        var items = await admin.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems");

        // One-off tasks, always Open: the engine is the only thing that creates an occurrence.
        items.Items.Count.ShouldBe(2);
        items.Items.ShouldAllBe(item => item.Status == WorkItemStatus.Open && item.ResponsibilityId == null);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_missing_required_mapping_is_refused_before_anything_is_written(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var commit = await admin.PostAsync(
            "/api/v1/imports/entities/commit",
            Csv("Name,Type\nAcme,Customer\n", mapping: Mapping((ImportFields.Name, "Name"))));

        commit.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var entities = await admin.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities");
        entities.TotalCount.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_file_over_the_row_cap_is_refused_before_anything_is_written(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(
            provider,
            settings: new Dictionary<string, string> { ["Import:MaxRows"] = "2" });

        var admin = await app.SignInAsAdminAsync();

        var commit = await admin.PostAsync(
            "/api/v1/imports/entities/commit",
            Csv(
                "Name,Type\nOne,Customer\nTwo,Customer\nThree,Customer\n",
                mapping: Mapping((ImportFields.Name, "Name"), (ImportFields.Type, "Type"))));

        commit.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var entities = await admin.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities");
        entities.TotalCount.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Importing_is_administrator_only(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();

        var response = await member.PostAsync(
            "/api/v1/imports/entities/preview",
            Csv("Name,Type\nAcme,Customer\n"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
