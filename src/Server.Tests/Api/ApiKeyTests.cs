using System.Net;
using Everdue.Server.Application.ApiKeys;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.ApiKeys;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// API keys.
///
/// The claim that matters most is the one about containment: a key is confined to an <em>endpoint allow-list</em>,
/// not to a role, so a leaked key cannot create a user even when the person it acts as is an administrator. That is
/// asserted route by route rather than argued.
/// </summary>
public class ApiKeyTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static async Task<(HttpClient Client, string Token)> AKeyAsync(
        EverdueApp app,
        HttpClient admin,
        ApiKeyScope scope = ApiKeyScope.ReadWrite)
    {
        var created = await admin.PostJsonAsync<CreatedApiKeyDto>("/api/v1/api-keys", new
        {
            name = $"Test key {scope}",
            scope = scope.ToString(),
        });

        var client = app.NewClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, created.Token);

        return (client, created.Token);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_read_write_key_reads_and_writes_work(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var (client, token) = await AKeyAsync(app, admin);

        token.ShouldStartWith("evd_");

        await (await client.GetAsync("/api/v1/workitems")).ShouldBeSuccessAsync();

        var created = await client.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Created by a script",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        created.Title.ShouldBe("Created by a script");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_read_only_key_is_refused_on_every_write(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var (client, _) = await AKeyAsync(app, admin, ApiKeyScope.ReadOnly);

        await (await client.GetAsync("/api/v1/workitems")).ShouldBeSuccessAsync();

        var refused = await client.PostJsonAsync("/api/v1/workitems", new
        {
            title = "Should not exist",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await refused.ProblemCodeAsync()).ShouldBe("api_key_read_only");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_key_cannot_reach_administration_even_though_its_actor_is_an_administrator(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var (client, _) = await AKeyAsync(app, admin);

        // The actor is the bootstrap administrator, so a role check alone would let all of these through. The
        // allow-list is what stops them, and this is the test that says so.
        foreach (var url in new[]
                 {
                     "/api/v1/users",
                     "/api/v1/settings/tenant",
                     "/api/v1/settings/channels",
                     "/api/v1/api-keys",
                     "/api/v1/webhooks",
                     "/api/v1/notifications",
                     "/api/v1/entity-fields",
                 })
        {
            var response = await client.GetAsync(url);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{url} should be unreachable with an API key");
            (await response.ProblemCodeAsync()).ShouldBe("api_key_not_permitted");
        }

        // And the same for the write paths a key must never have. Posted as real multipart, so the refusal is the
        // gate's and not a media-type complaint from the endpoint.
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("Name,Type\nAcme,Customer\n"));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");

        var import = await client.PostAsync(
            "/api/v1/imports/entities/preview",
            new MultipartFormDataContent { { file, "file", "import.csv" } });

        import.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await import.ProblemCodeAsync()).ShouldBe("api_key_not_permitted");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_unknown_revoked_or_tampered_key_gets_401(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var created = await admin.PostJsonAsync<CreatedApiKeyDto>("/api/v1/api-keys", new
        {
            name = "Doomed",
            scope = nameof(ApiKeyScope.ReadOnly),
        });

        HttpClient WithToken(string token)
        {
            var client = app.NewClient();
            client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, token);
            return client;
        }

        // Shaped like ours but never issued.
        (await WithToken("evd_000000000000_" + new string('a', 64)).GetAsync("/api/v1/workitems"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Not shaped like ours at all.
        (await WithToken("nonsense").GetAsync("/api/v1/workitems")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The right prefix, the wrong secret — the case a prefix index alone would have let through.
        var parts = created.Token.Split('_');
        (await WithToken($"{parts[0]}_{parts[1]}_{new string('b', 64)}").GetAsync("/api/v1/workitems"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var valid = WithToken(created.Token);
        await (await valid.GetAsync("/api/v1/workitems")).ShouldBeSuccessAsync();

        await (await admin.DeleteAsync($"/api/v1/api-keys/{created.Key.Id}")).ShouldBeSuccessAsync();

        // Revocation is immediate: the store reads RevokedAt on every authentication.
        (await valid.GetAsync("/api/v1/workitems")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_secret_is_never_stored_and_never_returned_again(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var created = await admin.PostJsonAsync<CreatedApiKeyDto>("/api/v1/api-keys", new
        {
            name = "Only once",
            scope = nameof(ApiKeyScope.ReadOnly),
        });

        var secret = created.Token.Split('_')[2];

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var stored = await db.ApiKeys.AsNoTracking().SingleAsync(key => key.Id == created.Key.Id);

            stored.KeyHash.ShouldNotContain(secret);
            stored.KeyPrefix.ShouldNotBe(secret);

            // The prefix is public by design; the secret half is only ever a hash.
            stored.KeyHash.ShouldBe(ApiKeyToken.Hash(secret));
        });

        var listed = await admin.GetAsync("/api/v1/api-keys");
        var body = await listed.Content.ReadAsStringAsync();

        body.ShouldNotContain(secret);
        body.ShouldContain(created.Key.KeyPrefix);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_write_made_with_a_key_is_attributed_to_its_actor_and_names_the_key(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var created = await admin.PostJsonAsync<CreatedApiKeyDto>("/api/v1/api-keys", new
        {
            name = "Attribution",
            scope = nameof(ApiKeyScope.ReadWrite),
        });

        var client = app.NewClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, created.Token);

        var item = await client.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "From the integration",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        var detail = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{item.Id}");
        var creation = detail.Events.Single(e => e.EventType == WorkItemEventType.Created);

        // The ledger's "who did this" is a person, never null — and the payload says which credential.
        creation.UserId.ShouldBe(ownerId);
        creation.DataJson.ShouldNotBeNull();
        creation.DataJson!.ShouldContain(created.Key.Id.ToString());
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_key_whose_actor_is_deactivated_stops_working(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var created = await admin.PostJsonAsync<CreatedApiKeyDto>("/api/v1/api-keys", new
        {
            name = "Acting as a member",
            scope = nameof(ApiKeyScope.ReadOnly),
            actorUserId = memberId,
        });

        var client = app.NewClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, created.Token);

        await (await client.GetAsync("/api/v1/workitems")).ShouldBeSuccessAsync();

        await admin.PutJsonAsync<UserDto>($"/api/v1/users/{memberId}", new
        {
            displayName = "Member",
            role = nameof(UserRole.Member),
            active = false,
        });

        // A key must not outlive its actor's access.
        (await client.GetAsync("/api/v1/workitems")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Key_management_is_administrator_only(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();

        (await member.GetAsync("/api/v1/api-keys")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var attempt = await member.PostJsonAsync("/api/v1/api-keys", new
        {
            name = "Mine",
            scope = nameof(ApiKeyScope.ReadWrite),
        });

        attempt.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
