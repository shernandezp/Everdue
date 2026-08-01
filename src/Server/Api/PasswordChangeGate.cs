using Everdue.Server.Infrastructure.Identity;

namespace Everdue.Server.Api;

/// <summary>
/// The bootstrap admin (and anyone an administrator has just reset) must change their password
/// before the rest of the API opens up. Enforced here rather than in the SPA, because the SPA is
/// not a security boundary.
/// </summary>
public sealed class PasswordChangeGate(RequestDelegate next)
{
    private static readonly string[] AlwaysAllowed =
    [
        "/api/v1/auth/me",
        "/api/v1/auth/logout",
        "/api/v1/auth/password",
        "/api/v1/auth/login",

        // Which sign-in methods exist is public information — the login screen asks before anybody
        // is signed in, and somebody stuck on the forced-change screen should still see the answer.
        "/api/v1/auth/providers",
        "/api/v1/auth/external",

        // Same reasoning: which languages exist, and what the licence is, are public facts the app shows on screens
        // somebody sees before they have a usable password. Both are anonymous endpoints anyway — being 403'd out of
        // them once a cookie exists would be the odd behaviour.
        "/api/v1/languages",
        "/api/v1/about",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        // An externally authenticated session is let through: asking somebody to change a password
        // they have never seen is a dead end. The flag itself stays set, so the temporary password
        // is still unusable until it is changed.
        var externallyAuthenticated = context.User.HasClaim(c => c.Type == ExternalLoginService.ExternalClaimType);

        if (path.StartsWithSegments("/api")
            && !externallyAuthenticated
            && context.User.HasClaim(c => c.Type == CurrentUser.MustChangePasswordClaim)
            && !AlwaysAllowed.Any(allowed => path.StartsWithSegments(allowed)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://everdue.app/problems/password_change_required",
                title = "Password change required",
                status = StatusCodes.Status403Forbidden,
                detail = "This account must set a new password before using Everdue.",
                code = "password_change_required",
            });

            return;
        }

        await next(context);
    }
}
