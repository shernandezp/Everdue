using System.Security.Claims;
using System.Text.Encodings.Web;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Infrastructure.ApiKeys;

public sealed class ApiKeySchemeOptions : AuthenticationSchemeOptions;

/// <summary>
/// Authenticates <c>X-Api-Key</c>.
///
/// <para>One header, no <c>Authorization: Bearer</c> alias. Two ways to authenticate is two things to get
/// wrong, and <c>Bearer</c> invites the assumption that a JWT would work.</para>
///
/// <para>The principal it issues is a <em>person</em>, not a key: <c>NameIdentifier</c> is the key's
/// <see cref="ApiKey.ActorUserId"/> and the role is that person's, so every write lands in the ledger
/// attributed to somebody real. What stops that from making a key an admin credential is
/// <see cref="ApiKeyGate"/>: which endpoints a key may reach is an allow-list, not a role.</para>
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeySchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IApiKeyStore keys,
    IUserDirectory users,
    ITenantContext tenantContext) : AuthenticationHandler<ApiKeySchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "ApiKey";

    public const string HeaderName = "X-Api-Key";

    /// <summary>Carries the key's id so every event it writes can name it.</summary>
    public const string KeyIdClaim = "everdue:apikey";

    /// <summary>Carries the scope, which <see cref="ApiKeyGate"/> reads to refuse writes on a read-only key.</summary>
    public const string ScopeClaim = "everdue:apikey_scope";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var presented) || presented.Count == 0)
        {
            // No opinion, rather than a failure: the cookie scheme gets its turn on a browser request.
            return AuthenticateResult.NoResult();
        }

        var info = await keys.AuthenticateAsync(presented.ToString(), Context.RequestAborted);

        if (info is null)
        {
            return AuthenticateResult.Fail("The API key is not valid.");
        }

        // The store reads the key table with the tenant filter ignored, because authentication runs before the
        // tenant is known. This instance serves exactly one tenant, so a key belonging to another one must be
        // refused here rather than going on to read the wrong tenant's rows — cheap now, and the line that stops
        // the hosted version from inheriting a cross-tenant hole.
        if (tenantContext.IsResolved && info.TenantId != tenantContext.TenantId)
        {
            Logger.LogWarning("API key {KeyId} belongs to tenant {TenantId}, which this instance does not serve.", info.KeyId, info.TenantId);
            return AuthenticateResult.Fail("The API key is not valid.");
        }

        var actor = await users.FindAsync(info.ActorUserId, Context.RequestAborted);

        if (actor is null || !actor.Active)
        {
            // The person the key acts as has left. A key must not outlive its actor's access.
            return AuthenticateResult.Fail("The API key's actor is no longer active.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()),
                new Claim(ClaimTypes.Role, actor.Role.ToString()),
                new Claim(CurrentUser.DisplayNameClaim, actor.DisplayName),
                new Claim(CurrentUser.LanguageClaim, actor.PreferredLanguage ?? string.Empty),
                new Claim(KeyIdClaim, info.KeyId.ToString()),
                new Claim(ScopeClaim, info.Scope.ToString()),
            ],
            SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>An API answers with a status code. There is no sign-in page to send a script to.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
