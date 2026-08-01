using System.Net;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// The hardening that does not depend on the app having no bugs: headers that remove whole classes
/// of attack, and a limit on the one endpoint an unauthenticated caller can reach.
/// </summary>
public class SecurityHardeningTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Every_response_carries_the_hardening_headers(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = app.NewClient();

        var response = await client.GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("Referrer-Policy").ShouldContain("no-referrer");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        csp.ShouldContain("default-src 'self'");
        csp.ShouldContain("frame-ancestors 'none'");
        csp.ShouldContain("object-src 'none'");

        // Inline script is the half of CSP that matters for XSS, and the SPA does not need it.
        csp.ShouldNotContain("script-src 'self' 'unsafe-inline'");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task HSTS_is_not_announced_from_a_plain_http_install(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);

        // Default posture: TLS terminates at a proxy, so promising HSTS here would lock users out.
        var response = await app.NewClient().GetAsync("/health");
        response.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();

        await using var secured = await EverdueApp.StartAsync(provider, new Dictionary<string, string>
        {
            ["Security:RequireHttps"] = "true",
        });

        var hardened = await secured.NewClient().GetAsync("/health");
        hardened.Headers.GetValues("Strict-Transport-Security").Single().ShouldContain("max-age=31536000");
    }

    /// <summary>
    /// Account lockout stops one account being hammered. This stops one password being tried against
    /// every account, which lockout cannot see because each account only fails once.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Sign_in_attempts_are_rate_limited(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, new Dictionary<string, string>
        {
            ["Security:LoginAttemptsPerMinute"] = "5",
        });

        var client = app.NewClient();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var response = await client.PostJsonAsync("/api/v1/auth/login", new
            {
                email = $"sprayed{attempt}@everdue.test",
                password = "not-the-password",
            });

            statuses.Add(response.StatusCode);
        }

        statuses.Count(s => s == HttpStatusCode.Unauthorized).ShouldBe(5);
        statuses.Count(s => s == HttpStatusCode.TooManyRequests).ShouldBe(3);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_limit_does_not_apply_to_signed_in_traffic(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, new Dictionary<string, string>
        {
            ["Security:LoginAttemptsPerMinute"] = "5",
        });

        var client = await app.SignInAsAdminAsync();

        // Normal use makes far more requests than the sign-in window allows; only /auth/login is limited.
        for (var request = 0; request < 20; request++)
        {
            (await client.GetAsync("/api/v1/workitems")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }
}
