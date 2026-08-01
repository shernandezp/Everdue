using Everdue.Server.Application.Contracts;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Api.Endpoints;

public static class ExternalAuthEndpoints
{
    /// <summary>
    /// Where the browser lands after a successful external sign-in.
    ///
    /// Deliberately **not** the board. The request immediately following a cross-site navigation does
    /// not carry a SameSite=Strict cookie, so redirecting straight into the app would show the login
    /// screen again with a perfectly valid session sitting in the cookie jar. This route is the
    /// static SPA shell, which needs no cookie; the app then calls /auth/me as a same-origin fetch,
    /// which does carry it. That is what lets the strict cookie posture survive Google sign-in.
    /// </summary>
    private const string CompletePath = "/login/complete";

    private const string FailurePath = "/login?error=external_login_failed";

    public static IEndpointRouteBuilder MapExternalAuthEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth");

        group.MapGet("/providers", (IOptions<GoogleAuthOptions> google)
                => Results.Ok(new AuthProvidersDto(google.Value.IsConfigured)))
            .AllowAnonymous()
            .WithSummary("Lets the login screen render the Google button only where it can actually work.")
            .Produces<AuthProvidersDto>();

        group.MapGet("/external/google/start", (
                [AsParameters] ExternalStartRequest request,
                IOptions<GoogleAuthOptions> google,
                HttpContext context) =>
            {
                if (!google.Value.IsConfigured)
                {
                    return Results.NotFound();
                }

                var properties = new AuthenticationProperties
                {
                    // Only a path, never an absolute URL: an open redirect on the sign-in route is
                    // the classic way to turn a login page into a phishing hop.
                    RedirectUri = $"/api/v1/auth/external/google/callback?returnUrl={Uri.EscapeDataString(SafeReturnUrl(request.ReturnUrl))}",
                };

                return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
            })
            .AllowAnonymous()
            .RequireRateLimiting(ApiPolicies.AuthRateLimit)
            .WithSummary("Starts the Google flow. 404 when Google is not configured on this installation.");

        group.MapGet("/external/google/callback", async (
                [AsParameters] ExternalStartRequest request,
                HttpContext context,
                SignInManager<AppUser> signInManager,
                ExternalLoginService externalLogins,
                CancellationToken cancellationToken) =>
            {
                // Reads the short-lived external cookie the Google handler wrote, which is why the
                // handler signs into IdentityConstants.ExternalScheme rather than the app cookie.
                var info = await signInManager.GetExternalLoginInfoAsync();

                if (info is null)
                {
                    return Results.Redirect(FailurePath);
                }

                var outcome = await externalLogins.SignInAsync(info, cancellationToken);

                // The external cookie has done its job either way; leaving it set would keep a
                // half-finished sign-in lying around the browser.
                await context.SignOutAsync(IdentityConstants.ExternalScheme);

                return outcome.Succeeded
                    ? Results.Redirect($"{CompletePath}?returnUrl={Uri.EscapeDataString(SafeReturnUrl(request.ReturnUrl))}")
                    : Results.Redirect(FailurePath);
            })
            .AllowAnonymous()
            .RequireRateLimiting(ApiPolicies.AuthRateLimit)
            .WithSummary("Signs in an existing, active, verified-e-mail user. Never creates one.");

        return api;
    }

    /// <summary>Relative, single-segment-rooted paths only — anything else goes to the board.</summary>
    private static string SafeReturnUrl(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl)
           && returnUrl.StartsWith('/')
           && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/board";
}

public sealed record ExternalStartRequest(string? ReturnUrl);
