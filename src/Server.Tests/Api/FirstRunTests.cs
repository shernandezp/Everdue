using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// The zero-config first run. The harness normally injects bootstrap credentials the way a configured
/// install would; these tests blank them to stand in for somebody who downloaded a release and simply
/// ran it — which must still yield an app that can be signed into.
/// </summary>
public class FirstRunTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static Dictionary<string, string> NoBootstrap => new()
    {
        ["Bootstrap:AdminEmail"] = "",
        ["Bootstrap:AdminPassword"] = "",
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_fresh_database_with_no_bootstrap_config_still_gets_an_admin(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, NoBootstrap);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var admin = await db.Users.SingleAsync(u => u.Email == "admin@everdue.local");

            admin.Role.ShouldBe(UserRole.Admin);
            admin.Active.ShouldBeTrue();

            // The generated password is printed once in the log and nowhere else; the account must
            // not be able to keep it.
            admin.MustChangePassword.ShouldBeTrue();

            // A real credential, not an account with no password: the log's password can actually
            // be typed in. (The value itself is deliberately unknowable here.)
            admin.PasswordHash.ShouldNotBeNullOrWhiteSpace();
        });
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Configured_bootstrap_credentials_win_over_the_generated_admin(TestProvider provider)
    {
        // The harness's defaults are the configured path — this pins that no generated account
        // appears beside the configured one.
        await using var app = await EverdueApp.StartAsync(provider);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            (await db.Users.AnyAsync(u => u.Email == "admin@everdue.local")).ShouldBeFalse();
            (await db.Users.AnyAsync(u => u.Email == EverdueApp.AdminEmail)).ShouldBeTrue();
        });
    }
}
