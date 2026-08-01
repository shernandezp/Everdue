using System.Net;
using System.Security.Claims;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Acceptance criterion 11. The OAuth round trip itself belongs to the framework; what is asserted
/// here is the decision Everdue makes with the identity it comes back with — which is the part that
/// would let the wrong person in.
/// </summary>
public class ExternalLoginTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static readonly Dictionary<string, string> GoogleConfigured = new()
    {
        ["Auth:Google:ClientId"] = "test-client-id",
        ["Auth:Google:ClientSecret"] = "test-client-secret",
    };

    private static ExternalLoginInfo Info(string email, bool verified = true, string key = "google-subject-1")
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, key),
            new Claim(ClaimTypes.Email, email),
            new Claim("email_verified", verified ? "true" : "false"),
        ], GoogleDefaults.AuthenticationScheme);

        return new ExternalLoginInfo(
            new ClaimsPrincipal(identity),
            GoogleDefaults.AuthenticationScheme,
            key,
            GoogleDefaults.DisplayName);
    }

    /// <summary>
    /// Runs the decision outside a request, with a context supplied: issuing the cookie is the
    /// framework's job and needs one, while everything asserted here happens before that.
    /// </summary>
    private static Task<ExternalSignInOutcome> SignInAsync(EverdueApp app, ExternalLoginInfo info)
        => app.ScopedAsync(services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext =
                new DefaultHttpContext { RequestServices = services };

            return services.GetRequiredService<ExternalLoginService>().SignInAsync(info);
        });

    /// <summary>The button only appears where it can actually work.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_provider_list_reflects_whether_google_is_configured(TestProvider provider)
    {
        await using var unconfigured = await EverdueApp.StartAsync(provider);
        (await unconfigured.NewClient().GetJsonAsync<AuthProvidersDto>("/api/v1/auth/providers")).Google.ShouldBeFalse();

        await using var configured = await EverdueApp.StartAsync(provider, GoogleConfigured);
        (await configured.NewClient().GetJsonAsync<AuthProvidersDto>("/api/v1/auth/providers")).Google.ShouldBeTrue();
    }

    /// <summary>An unconfigured installation 404s the route rather than failing inside a handler that is not there.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_start_route_is_absent_when_google_is_not_configured(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);

        (await app.NewClient().GetAsync("/api/v1/auth/external/google/start"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_existing_active_user_is_signed_in_and_the_login_is_linked(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, GoogleConfigured);

        var outcome = await SignInAsync(app, Info(EverdueApp.AdminEmail));
        outcome.Succeeded.ShouldBeTrue();

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var logins = await db.UserLogins.ToListAsync();

            logins.ShouldContain(l => l.LoginProvider == GoogleDefaults.AuthenticationScheme);
        });
    }

    /// <summary>No auto-provisioning: an external provider is a way to authenticate, not an invitation.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_google_account_with_no_matching_user_is_refused_and_creates_nobody(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, GoogleConfigured);

        var before = await app.ScopedAsync(services =>
            services.GetRequiredService<EverdueDbContext>().Users.CountAsync());

        var outcome = await SignInAsync(app, Info("stranger@example.com"));

        outcome.Succeeded.ShouldBeFalse();
        outcome.Failure.ShouldBe(ExternalSignInFailure.NoMatchingUser);

        var after = await app.ScopedAsync(services =>
            services.GetRequiredService<EverdueDbContext>().Users.CountAsync());

        after.ShouldBe(before);
    }

    /// <summary>
    /// Without <c>email_verified</c>, "the address matches" only means somebody typed it into a
    /// Google profile.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_unverified_google_address_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, GoogleConfigured);

        var outcome = await SignInAsync(app, Info(EverdueApp.AdminEmail, verified: false));

        outcome.Succeeded.ShouldBeFalse();
        outcome.Failure.ShouldBe(ExternalSignInFailure.EmailUnverified);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_deactivated_user_cannot_sign_in_with_google(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, GoogleConfigured);
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == memberId);
            user.Active = false;
            await db.SaveChangesAsync();
        });

        var outcome = await SignInAsync(app, Info(EverdueApp.MemberEmail));

        outcome.Succeeded.ShouldBeFalse();
        outcome.Failure.ShouldBe(ExternalSignInFailure.UserInactive);
    }

    /// <summary>Password sign-in is untouched by any of this — it is the rescue path if Google config breaks.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Password_login_still_works_with_google_configured(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, GoogleConfigured);

        var client = await app.SignInAsAdminAsync();
        (await client.GetJsonAsync<CurrentUserDto>("/api/v1/auth/me")).Email.ShouldBe(EverdueApp.AdminEmail);
    }
}
