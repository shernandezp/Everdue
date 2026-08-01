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
/// Acceptance criterion 7: an export of Spanish entity names, re-imported into an empty install, reproduces the same
/// entities — custom field values included — with zero failed rows.
///
/// This is the criterion that ties the two halves of the version together, and the one most likely to break
/// silently: the export writes a header per definition and the import matches headers by name, so a change to
/// either side's naming would leave both features working and the round trip broken.
/// </summary>
public class ExportImportRoundTripTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static MultipartFormDataContent AsUpload(byte[] content, string mapping)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        return new MultipartFormDataContent
        {
            { file, "file", "entities.csv" },
            { new StringContent(mapping), "mapping" },
        };
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Entities_survive_an_export_and_a_re_import(TestProvider provider)
    {
        (string Name, EntityType Type, string? Manager)[] originals =
        [
            ("Acme Distribución", EntityType.Customer, "Ana Restrepo"),
            ("Ferretería El Progreso", EntityType.Customer, "Luisa Franco"),
            ("Comercial Ríos, S.A.", EntityType.Customer, "Carlos Méndez"),
            ("Suministros Andinos", EntityType.Supplier, null),
        ];

        byte[] exported;
        string mapping;

        // ── The install somebody is migrating away from ────────────────────────────────────────────────
        await using (var source = await EverdueApp.StartAsync(provider))
        {
            var admin = await source.SignInAsAdminAsync();

            var field = await admin.PostJsonAsync<EntityFieldDefDto>("/api/v1/entity-fields", new
            {
                entityType = nameof(EntityType.Customer),
                name = "Account manager",
                fieldType = nameof(EntityFieldType.Text),
            });

            foreach (var (name, type, manager) in originals)
            {
                await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new
                {
                    name,
                    type = type.ToString(),
                    customFields = manager is null
                        ? new Dictionary<string, string>()
                        : new Dictionary<string, string> { [field.Id.ToString()] = manager },
                });
            }

            var response = await admin.GetAsync("/api/v1/exports/raw/entities");
            await response.ShouldBeSuccessAsync();

            exported = await response.Content.ReadAsByteArrayAsync();

            // The header the export writes for a custom field, mapped back onto the same definition. A human does
            // this in the wizard; the test does it by name, which is exactly what the wizard's suggestion matches on.
            mapping = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                [ImportFields.Name] = "name",
                [ImportFields.Type] = "type",
                [ImportFields.Active] = "active",
                [ImportFields.Custom(field.Id)] = $"{EntityType.Customer}:Account manager",
            });
        }

        // ── The empty install it is going to ──────────────────────────────────────────────────────────
        await using var target = await EverdueApp.StartAsync(provider);
        var targetAdmin = await target.SignInAsAdminAsync();

        // The definition has to exist first — an import populates fields, it does not invent them.
        var targetField = await targetAdmin.PostJsonAsync<EntityFieldDefDto>("/api/v1/entity-fields", new
        {
            entityType = nameof(EntityType.Customer),
            name = "Account manager",
            fieldType = nameof(EntityFieldType.Text),
        });

        var targetMapping = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [ImportFields.Name] = "name",
            [ImportFields.Type] = "type",
            [ImportFields.Active] = "active",
            [ImportFields.Custom(targetField.Id)] = $"{EntityType.Customer}:Account manager",
        });

        // The suggestion the wizard would offer, checked before the commit that uses it: the export header and the
        // import label differ in whitespace and case, and only the normaliser makes them the same thing.
        var previewUpload = AsUpload(exported, targetMapping);
        var preview = await targetAdmin.PostAsync("/api/v1/imports/entities/preview", previewUpload);
        await preview.ShouldBeSuccessAsync();

        var previewed = (await preview.Content.ReadFromJsonAsync<ImportPreviewDto>(EverdueApp.Json))!;
        previewed.TotalRows.ShouldBe(originals.Length);
        previewed.SuggestedMapping[ImportFields.Name].ShouldBe("name");
        previewed.SuggestedMapping.ShouldContainKey(ImportFields.Custom(targetField.Id));
        previewed.Rows.ShouldAllBe(row => row.Error == null);

        var commit = await targetAdmin.PostAsync("/api/v1/imports/entities/commit", AsUpload(exported, targetMapping));
        await commit.ShouldBeSuccessAsync();

        var result = (await commit.Content.ReadFromJsonAsync<ImportResultDto>(EverdueApp.Json))!;
        result.Created.ShouldBe(originals.Length);
        result.Failed.ShouldBe(0);
        result.Skipped.ShouldBe(0);

        var reimported = await targetAdmin.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities?pageSize=100&includeInactive=true");
        reimported.TotalCount.ShouldBe(originals.Length);

        foreach (var (name, type, manager) in originals)
        {
            var entity = reimported.Items.SingleOrDefault(e => e.Name == name);

            // Accents survived the BOM and the encoding; the embedded comma survived the quoting.
            entity.ShouldNotBeNull($"'{name}' did not survive the round trip");
            entity!.Type.ShouldBe(type);

            var value = entity.CustomFields.SingleOrDefault(field => field.DefinitionId == targetField.Id)?.Value;
            value.ShouldBe(manager);
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Re_importing_the_same_file_twice_creates_nothing_the_second_time(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var content = new UTF8Encoding(true).GetBytes("name,type\nAcme Distribución,Customer\nComercial Ríos,Customer\n");

        var mapping = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [ImportFields.Name] = "name",
            [ImportFields.Type] = "type",
        });

        var first = await admin.PostAsync("/api/v1/imports/entities/commit", AsUpload(content, mapping));
        (await first.Content.ReadFromJsonAsync<ImportResultDto>(EverdueApp.Json))!.Created.ShouldBe(2);

        // Idempotent by way of "skip, never update" — which is what makes re-running a failed migration safe.
        var second = await admin.PostAsync("/api/v1/imports/entities/commit", AsUpload(content, mapping));
        var result = (await second.Content.ReadFromJsonAsync<ImportResultDto>(EverdueApp.Json))!;

        result.Created.ShouldBe(0);
        result.Skipped.ShouldBe(2);
        result.Failed.ShouldBe(0);
    }
}
