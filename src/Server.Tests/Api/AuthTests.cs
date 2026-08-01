using System.Net;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

public class AuthTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Wrong_credentials_and_unknown_accounts_are_answered_identically(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = app.NewClient();

        var wrongPassword = await client.PostJsonAsync("/api/v1/auth/login", new { email = EverdueApp.AdminEmail, password = "not-the-password" });
        var unknownUser = await client.PostJsonAsync("/api/v1/auth/login", new { email = "ghost@everdue.test", password = "not-the-password" });

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownUser.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Same answer either way: the API must not confirm whether an account exists.
        // (Compared field by field — the trace id is per-request and is not part of the answer.)
        (await Detail(wrongPassword)).ShouldBe(await Detail(unknownUser));
        (await wrongPassword.ProblemCodeAsync()).ShouldBe(await unknownUser.ProblemCodeAsync());

        static async Task<string?> Detail(HttpResponseMessage response)
        {
            using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.GetProperty("detail").GetString();
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_deactivated_user_cannot_sign_in_but_keeps_their_history(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var member = await app.SignInAsMemberAsync();
        var task = await member.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Left behind",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PutJsonAsync<UserDto>($"/api/v1/users/{memberId}", new
        {
            displayName = "Member",
            role = nameof(UserRole.Member),
            active = false,
        });

        var retry = await app.NewClient().PostJsonAsync("/api/v1/auth/login", new { email = EverdueApp.MemberEmail, password = EverdueApp.MemberPassword });
        retry.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var stillThere = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{task.Id}");
        stillThere.Item.OwnerUserId.ShouldBe(memberId);
        stillThere.Item.OwnerDisplayName.ShouldBe("Member");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_bootstrap_admin_must_change_their_password_before_using_anything_else(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);

        // Put the flag back: EverdueApp clears it for every other test.
        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var admin = await db.Users.SingleAsync(u => u.Email == EverdueApp.AdminEmail);
            admin.MustChangePassword = true;
            await db.SaveChangesAsync();
        });

        var client = await app.SignInAsAdminAsync();

        var me = await client.GetJsonAsync<CurrentUserDto>("/api/v1/auth/me");
        me.MustChangePassword.ShouldBeTrue();

        var blocked = await client.GetAsync("/api/v1/workitems");
        blocked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await blocked.ProblemCodeAsync()).ShouldBe("password_change_required");

        var change = await client.PostJsonAsync("/api/v1/auth/password", new
        {
            currentPassword = EverdueApp.AdminPassword,
            newPassword = "Everdue2026Changed!",
        });

        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/v1/workitems")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A forced change that accepts the same password is not a change. Identity has no such rule of
    /// its own, so the seeded bootstrap password could otherwise survive the first login.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_password_cannot_be_changed_to_the_one_already_in_use(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var reused = await client.PostJsonAsync("/api/v1/auth/password", new
        {
            currentPassword = EverdueApp.AdminPassword,
            newPassword = EverdueApp.AdminPassword,
        });

        reused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await reused.ProblemCodeAsync()).ShouldBe("validation_failed");
        (await reused.Content.ReadAsStringAsync()).ShouldContain("different");

        // A genuinely new one still works.
        (await client.PostJsonAsync("/api/v1/auth/password", new
        {
            currentPassword = EverdueApp.AdminPassword,
            newPassword = "Everdue2026Different!",
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_admin_reset_cannot_reuse_the_users_current_password(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var reused = await admin.PostJsonAsync($"/api/v1/users/{memberId}/password", new
        {
            newPassword = EverdueApp.MemberPassword,
        });

        reused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await admin.PostJsonAsync($"/api/v1/users/{memberId}/password", new
        {
            newPassword = "Everdue2026Reset!",
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Language_falls_back_to_the_tenant_default_and_a_preference_wins(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var initial = await client.GetJsonAsync<CurrentUserDto>("/api/v1/auth/me");
        initial.Language.ShouldBe(Languages.Spanish); // the tenant default
        initial.Tenant.TimeZoneId.ShouldBe("America/Bogota");

        var updated = await client.PutJsonAsync<CurrentUserDto>("/api/v1/auth/profile", new
        {
            displayName = "Administrator",
            preferredLanguage = Languages.English,
        });

        updated.Language.ShouldBe(Languages.English);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Signing_out_clears_the_cookie(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        (await client.GetAsync("/api/v1/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.PostJsonAsync("/api/v1/auth/logout")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/v1/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Tenant_settings_reject_an_unknown_time_zone_and_an_unsupported_language(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var badZone = await client.PutJsonAsync("/api/v1/settings/tenant", new
        {
            name = "Everdue tests",
            timeZoneId = "Mars/Olympus_Mons",
            digestHourLocal = 7,
            defaultLanguage = Languages.Spanish,
        });

        badZone.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var badLanguage = await client.PutJsonAsync("/api/v1/settings/tenant", new
        {
            name = "Everdue tests",
            timeZoneId = "America/Bogota",
            digestHourLocal = 7,
            defaultLanguage = "fr",
        });

        badLanguage.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var good = await client.PutJsonAsync<TenantSettingsDto>("/api/v1/settings/tenant", new
        {
            name = "Everdue",
            timeZoneId = "Europe/Madrid",
            digestHourLocal = 8,
            defaultLanguage = Languages.English,
        });

        good.TimeZoneId.ShouldBe("Europe/Madrid");
        good.DigestHourLocal.ShouldBe(8);
    }
}
