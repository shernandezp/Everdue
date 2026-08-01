using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.ApiKeys;
using Microsoft.AspNetCore.RateLimiting;

namespace Everdue.Server.Api;

// This lives in Api, not Infrastructure: it is middleware plus an endpoint convention, which is the same kind of
// thing as PasswordChangeGate next to it. The spec's file layout put it under Infrastructure/ApiKeys; that was
// wrong, and following it would have meant Infrastructure referencing ApiPolicies in the Api layer.

/// <summary>
/// Marks an endpoint as reachable with an API key. Absent, an API-key caller gets a 403 whatever its actor's
/// role is — which is what stops a key in a script's environment variable from being an admin credential.
/// </summary>
public sealed class AllowApiKeyAttribute : Attribute;

public static class AllowApiKeyExtensions
{
    /// <summary>
    /// Applied to the resource groups a programmatic caller has business with: work, entities, departments,
    /// responsibilities, comments, attachments, checklists, reports, insights and exports.
    ///
    /// Deliberately <em>not</em> applied to auth, users, tenant settings, channels, notifications, <c>/me</c>,
    /// imports, API keys or webhook management. A leaked key cannot create a user or read a channel secret.
    /// </summary>
    public static TBuilder AllowApiKey<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new AllowApiKeyAttribute());

        // Opting an endpoint into API keys is also what rate-limits them on it. The policy returns no limiter
        // for a cookie session, so a person clicking around is never rationed because a script was busy.
        builder.RequireRateLimiting(ApiPolicies.ApiKeyRateLimit);

        return builder;
    }
}

/// <summary>
/// Two refusals, both of which have to happen after routing (so the endpoint's metadata is known) and after
/// authentication (so the principal is known):
///
/// <list type="number">
/// <item>an API key on an endpoint that does not opt in — 403;</item>
/// <item>a <see cref="ApiKeyScope.ReadOnly"/> key on anything that is not a safe read — 403.</item>
/// </list>
///
/// Cookie sessions pass straight through: nothing here changes how a browser request behaves.
/// </summary>
public sealed class ApiKeyGate(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var keyIdClaim = context.User.FindFirst(ApiKeyAuthenticationHandler.KeyIdClaim);

        if (keyIdClaim is null)
        {
            await next(context);
            return;
        }

        var endpoint = context.GetEndpoint();

        if (endpoint?.Metadata.GetMetadata<AllowApiKeyAttribute>() is null)
        {
            await RefuseAsync(
                context,
                "api_key_not_permitted",
                "API keys cannot be used on this endpoint. It is reachable only by a signed-in user.");

            return;
        }

        var scope = context.User.FindFirst(ApiKeyAuthenticationHandler.ScopeClaim)?.Value;

        if (scope == nameof(ApiKeyScope.ReadOnly) && !IsRead(context.Request.Method))
        {
            await RefuseAsync(
                context,
                "api_key_read_only",
                "This API key is read-only. Create a read-write key to make changes.");

            return;
        }

        await next(context);
    }

    private static bool IsRead(string method)
        => HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    private static async Task RefuseAsync(HttpContext context, string code, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://everdue.app/problems/{code}",
            title = "Forbidden",
            status = StatusCodes.Status403Forbidden,
            detail,
            code,
        });
    }
}
