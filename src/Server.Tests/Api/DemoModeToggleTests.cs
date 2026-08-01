using System.Net;
using Everdue.Server.Application.ApiKeys;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.ApiKeys;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// The runtime demo-mode toggle — the one operation in Everdue that deletes the ledger.
///
/// Everything else in the suite pins that a miss survives. These tests pin the shape of the single, deliberate
/// exception: that it is genuinely total when it runs, that the caller keeps a way back in, and above all that
/// <strong>nothing at all happens</strong> unless both confirmations are right. A partial wipe on a mistyped
/// password would be the worst possible failure mode, so the refusal cases assert the ledger afterwards rather
/// than only the status code.
/// </summary>
public class DemoModeToggleTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    /// <summary>The tenant name <see cref="EverdueApp"/> configures — what the confirmation must match.</summary>
    private const string Workspace = "Everdue tests";

    private static object Body(bool enabled, string? confirmation = null, string? password = null)
        => new
        {
            enabled,
            confirmation = confirmation ?? Workspace,
            password = password ?? EverdueApp.AdminPassword,
        };

    /// <summary>A small but complete workspace: reference data, a responsibility, and a ledger with events in it.</summary>
    private static async Task ARealWorkspaceAsync(EverdueApp app, HttpClient admin)
    {
        var entity = await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "A real customer",
            type = nameof(EntityType.Customer),
        });

        await admin.PostJsonAsync<DepartmentDto>("/api/v1/departments", new { name = "A real department" });

        await app.SeedAsync((ledger, owner) =>
        {
            var responsibility = ledger.Responsibility("A real obligation", owner, entity.Id);

            var items = ledger.History(
                responsibility,
                count: 6,
                periodDays: 1,
                statusFor: index => index % 3 == 0 ? WorkItemStatus.Missed : WorkItemStatus.Completed);

            // A hold, so there are WorkItemEvents to delete as well as work items — the audit trail is a
            // separate table with its own foreign key, and "the ledger is gone" has to include it.
            ledger.Hold(items[1], HoldReason.WaitingCustomer, ledger.At(ledger.Today.AddDays(-2), 9));
        });
    }

    private static Task<int> WorkItemCountAsync(EverdueApp app)
        => app.ScopedAsync(services => services.GetRequiredService<EverdueDbContext>().WorkItems.CountAsync());

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Turning_demo_mode_on_replaces_the_workspace_with_seeded_history(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await ARealWorkspaceAsync(app, admin);
        (await WorkItemCountAsync(app)).ShouldBeGreaterThan(0);

        var result = await admin.PostJsonAsync<DemoModeResultDto>("/api/v1/settings/demo", Body(enabled: true));

        result.Status.Enabled.ShouldBeTrue();
        result.Deleted.WorkItems.ShouldBeGreaterThan(0);
        result.Seeded.ShouldNotBeNull();
        result.Seeded!.Responsibilities.ShouldBeGreaterThan(0);
        result.Seeded.Occurrences.ShouldBeGreaterThan(0);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();

            // The real workspace is gone, root and branch — not merged with, not left beside.
            (await db.Entities.CountAsync(e => e.Name == "A real customer")).ShouldBe(0);
            (await db.Departments.CountAsync(d => d.Name == "A real department")).ShouldBe(0);
            (await db.Responsibilities.CountAsync(r => r.Title == "A real obligation")).ShouldBe(0);

            // And the demo really is there, with the mix of outcomes the reports need.
            var occurrences = await db.WorkItems.Where(w => w.ResponsibilityId != null).Select(w => w.Status).ToListAsync();
            occurrences.Count.ShouldBeGreaterThan(200);
            occurrences.Count(s => s == WorkItemStatus.Completed).ShouldBeGreaterThan(0);
            occurrences.Count(s => s.CountsAsMissed()).ShouldBeGreaterThan(0);

            (await db.Tenants.SingleAsync()).DemoMode.ShouldBeTrue();
        });

        // The real member's account went with everything else, so this signs in as a seeded one — which also
        // pins the thing the whole feature rests on: the demo accounts can actually be used, with the password
        // the response just reported, and without being asked to change it first.
        var demoMember = await app.SignInAsync("carlos@demo.everdue.app", result.Seeded.Password);

        // The flag reaches every signed-in client, not just the admin settings screen. A member who cannot see
        // that badge is exactly the person who would put real work into an install full of invented history.
        //
        // /me is asserted as well as /settings/tenant, and it is the one that matters: the client draws the
        // badge from the session, not from the settings screen a member cannot even reach. These two answers
        // came from two different pieces of mapping code once, and only one of them had been taught the field.
        (await demoMember.GetJsonAsync<TenantSettingsDto>("/api/v1/settings/tenant")).DemoMode.ShouldBeTrue();
        (await demoMember.GetJsonAsync<CurrentUserDto>("/api/v1/auth/me")).Tenant.DemoMode.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Turning_demo_mode_off_leaves_an_empty_workspace_and_only_the_caller(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await admin.PostJsonAsync<DemoModeResultDto>("/api/v1/settings/demo", Body(enabled: true));

        var result = await admin.PostJsonAsync<DemoModeResultDto>("/api/v1/settings/demo", Body(enabled: false));

        result.Status.Enabled.ShouldBeFalse();
        result.Seeded.ShouldBeNull();
        result.Deleted.WorkItems.ShouldBeGreaterThan(0);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();

            (await db.WorkItems.CountAsync()).ShouldBe(0);
            (await db.WorkItemEvents.CountAsync()).ShouldBe(0);
            (await db.Responsibilities.CountAsync()).ShouldBe(0);
            (await db.Entities.CountAsync()).ShouldBe(0);
            (await db.Departments.CountAsync()).ShouldBe(0);
            (await db.Notifications.CountAsync()).ShouldBe(0);
            (await db.ChecklistItems.CountAsync()).ShouldBe(0);

            // The one thing that must survive: an install nobody can sign into is not a reset, it is a brick.
            var users = await db.Users.Select(u => u.Email).ToListAsync();
            users.ShouldBe([EverdueApp.AdminEmail]);

            (await db.Tenants.SingleAsync()).DemoMode.ShouldBeFalse();
        });

        // And the session still works afterwards — the account was kept, so nothing about the cookie changed.
        (await admin.GetJsonAsync<TenantSettingsDto>("/api/v1/settings/tenant")).DemoMode.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_wrong_confirmation_changes_nothing(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await ARealWorkspaceAsync(app, admin);
        var before = await WorkItemCountAsync(app);

        var response = await admin.PostJsonAsync("/api/v1/settings/demo", Body(enabled: true, confirmation: "everdue tests"));

        // Deliberately case-sensitive: "close enough" is not a confirmation.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).ShouldBe("validation_failed");

        (await WorkItemCountAsync(app)).ShouldBe(before);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_wrong_password_changes_nothing(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await ARealWorkspaceAsync(app, admin);
        var before = await WorkItemCountAsync(app);

        var response = await admin.PostJsonAsync("/api/v1/settings/demo", Body(enabled: true, password: "NotMyPassword1!"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await WorkItemCountAsync(app)).ShouldBe(before);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();

            (await db.Entities.CountAsync(e => e.Name == "A real customer")).ShouldBe(1);
            (await db.Users.CountAsync()).ShouldBe(2);
        });
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_member_cannot_reach_it(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        await ARealWorkspaceAsync(app, admin);

        var member = await app.SignInAsMemberAsync();

        (await member.GetAsync("/api/v1/settings/demo")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.PostJsonAsync("/api/v1/settings/demo", Body(enabled: true))).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        (await WorkItemCountAsync(app)).ShouldBeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_api_key_cannot_reach_it_however_privileged_its_actor(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        await ARealWorkspaceAsync(app, admin);

        // The key acts as the administrator. Reachability is an endpoint allow-list, not a role — and this
        // endpoint is not on it, so no script wipes a tenant.
        var created = await admin.PostJsonAsync<CreatedApiKeyDto>("/api/v1/api-keys", new
        {
            name = "Demo test key",
            scope = nameof(ApiKeyScope.ReadWrite),
        });

        var key = app.NewClient();
        key.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, created.Token);

        (await key.PostJsonAsync("/api/v1/settings/demo", Body(enabled: true))).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        (await WorkItemCountAsync(app)).ShouldBeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_install_with_the_reset_disabled_does_not_have_the_feature(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(
            provider,
            new Dictionary<string, string> { ["Demo:AllowReset"] = "false" });

        var admin = await app.SignInAsAdminAsync();
        await ARealWorkspaceAsync(app, admin);

        // The status still answers — that is how the client knows to render nothing at all.
        var status = await admin.GetJsonAsync<DemoStatusDto>("/api/v1/settings/demo");
        status.ResetAllowed.ShouldBeFalse();

        // 404 rather than 403: the capability is absent from this install, not withheld from this caller.
        var response = await admin.PostJsonAsync("/api/v1/settings/demo", Body(enabled: true));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await WorkItemCountAsync(app)).ShouldBeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_status_reports_what_the_confirmation_must_say(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var status = await admin.GetJsonAsync<DemoStatusDto>("/api/v1/settings/demo");

        status.Enabled.ShouldBeFalse();
        status.ResetAllowed.ShouldBeTrue();

        // The dialog shows this string and the server compares against it; one source, so they cannot drift.
        status.ConfirmationPhrase.ShouldBe(Workspace);
        status.DemoPassword.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Attachment_bytes_go_with_the_rows(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await ARealWorkspaceAsync(app, admin);

        var item = await app.ScopedAsync(async services =>
            await services.GetRequiredService<EverdueDbContext>().WorkItems.OrderBy(w => w.CreatedAt).FirstAsync());

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("a photograph, more or less"u8.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "proof.png");

        var upload = await admin.PostAsync($"/api/v1/workitems/{item.Id}/attachments", content);
        await upload.ShouldBeSuccessAsync();

        var key = await app.ScopedAsync(async services =>
            await services.GetRequiredService<EverdueDbContext>().Attachments.Select(a => a.StorageKey).SingleAsync());

        await admin.PostJsonAsync<DemoModeResultDto>("/api/v1/settings/demo", Body(enabled: false));

        // A "wiped" install that still holds the photographs has not been wiped.
        await app.ScopedAsync(async services =>
        {
            (await services.GetRequiredService<EverdueDbContext>().Attachments.CountAsync()).ShouldBe(0);

            var store = services.GetRequiredService<Everdue.Server.Application.Abstractions.IFileStore>();
            (await store.OpenReadAsync(key)).ShouldBeNull();
        });
    }
}
