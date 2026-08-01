using System.Net;
using System.Text;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// CSV export. The invariant under test is the drill-through invariant applied to files: an export contains exactly
/// the rows its screen shows for the same filters.
/// </summary>
public class ExportTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static async Task<(string Text, HttpResponseMessage Response)> CsvAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        await response.ShouldBeSuccessAsync();

        return (await response.Content.ReadAsStringAsync(), response);
    }

    private static string[] Lines(string csv)
        => csv.TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_work_item_export_contains_exactly_what_the_list_shows(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        for (var index = 0; index < 3; index++)
        {
            await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
            {
                title = $"Task {index}",
                ownerUserId = ownerId,
                dueDate = app.Clock.UtcNow.AddDays(index + 1),
            });
        }

        var completed = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Already done",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PostAsync($"/api/v1/workitems/{completed.Id}/complete", null);

        // Same filter on both, which is the whole claim.
        var list = await admin.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems?status=Open&pageSize=100");
        var (csv, response) = await CsvAsync(admin, "/api/v1/exports/workitems?status=Open");

        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");

        var lines = Lines(csv);
        (lines.Length - 1).ShouldBe(list.TotalCount);
        lines[0].ShouldStartWith("id,kind,");

        // And the completed one is genuinely absent, not merely uncounted.
        csv.ShouldNotContain("Already done");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_file_carries_a_byte_order_mark(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var response = await admin.GetAsync("/api/v1/exports/workitems");
        await response.ShouldBeSuccessAsync();

        var bytes = await response.Content.ReadAsByteArrayAsync();

        // Without it Excel on a Spanish machine renders "Guía" as mojibake, and opening the file in Excel is the
        // first thing anybody does with an export.
        bytes.Take(3).ShouldBe(new byte[] { 0xEF, 0xBB, 0xBF });
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_formula_is_neutralised_and_a_delimiter_is_quoted(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "=cmd|'/c calc'!A1",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Call Acme, then Ríos \"urgently\"",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(2),
        });

        var (csv, _) = await CsvAsync(admin, "/api/v1/exports/workitems");

        // OWASP: a leading =, +, -, @, tab or CR is prefixed so a spreadsheet treats it as text.
        csv.ShouldContain("'=cmd|");
        csv.ShouldNotContain(",=cmd|");

        // A cell containing the delimiter and a quote is quoted, with the quote doubled.
        csv.ShouldContain("\"Call Acme, then Ríos \"\"urgently\"\"\"");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_export_over_the_row_limit_is_refused_rather_than_truncated(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(
            provider,
            settings: new Dictionary<string, string> { ["Exports:MaxRows"] = "2" });

        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        for (var index = 0; index < 3; index++)
        {
            await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
            {
                title = $"Task {index}",
                ownerUserId = ownerId,
                dueDate = app.Clock.UtcNow.AddDays(index + 1),
            });
        }

        var response = await admin.GetAsync("/api/v1/exports/workitems");

        // A truncated file that looks complete is the worst possible input to a decision.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Narrow the filters");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Report_and_insight_exports_are_administrator_only(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();

        // An export never widens what its source endpoint allows.
        (await member.GetAsync("/api/v1/exports/reports/entity-health")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.GetAsync("/api/v1/exports/insights/compliance")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.GetAsync("/api/v1/exports/raw/workitems")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // But the work list is theirs, so its export is too.
        await (await member.GetAsync("/api/v1/exports/workitems")).ShouldBeSuccessAsync();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Every_report_and_insight_view_exports(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        foreach (var view in new[] { "entity-health", "neglect", "blocked-by-entity" })
        {
            var (csv, _) = await CsvAsync(admin, $"/api/v1/exports/reports/{view}");
            Lines(csv).Length.ShouldBeGreaterThanOrEqualTo(1);
        }

        foreach (var view in new[] { "compliance", "reliability", "concentration", "hold-aging" })
        {
            var (csv, _) = await CsvAsync(admin, $"/api/v1/exports/insights/{view}");
            Lines(csv).Length.ShouldBeGreaterThanOrEqualTo(1);
        }

        foreach (var table in new[] { "entities", "responsibilities", "workitems", "workitem-events", "comments", "checklist-items" })
        {
            var (csv, _) = await CsvAsync(admin, $"/api/v1/exports/raw/{table}");
            Lines(csv).Length.ShouldBeGreaterThanOrEqualTo(1);
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Nothing_exports_without_signing_in(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var anonymous = app.NewClient();

        // The export group carries no group-level authorization — each route asserts its own — so this is the check
        // that a route added later without one would fail.
        foreach (var url in new[]
                 {
                     "/api/v1/exports/workitems",
                     "/api/v1/exports/reports/entity-health",
                     "/api/v1/exports/insights/compliance",
                     "/api/v1/exports/raw/entities",
                 })
        {
            (await anonymous.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized, url);
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_unknown_view_or_table_is_a_bad_request(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        (await admin.GetAsync("/api/v1/exports/reports/whatever")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/exports/raw/users")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_raw_entity_export_round_trips_custom_fields(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var field = await admin.PostJsonAsync<EntityFieldDefDto>("/api/v1/entity-fields", new
        {
            entityType = nameof(EntityType.Customer),
            name = "Account manager",
            fieldType = nameof(EntityFieldType.Text),
        });

        await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "Ferretería El Progreso",
            type = nameof(EntityType.Customer),
            customFields = new Dictionary<string, string> { [field.Id.ToString()] = "Luisa Franco" },
        });

        var (csv, _) = await CsvAsync(admin, "/api/v1/exports/raw/entities");

        // The one place a custom field leaves the entity screen: a backup that cannot round-trip is not a backup.
        csv.ShouldContain("Customer:Account manager");
        csv.ShouldContain("Luisa Franco");
        csv.ShouldContain("Ferretería El Progreso");
    }
}
