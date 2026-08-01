using System.Security.Claims;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Everdue.Server.Infrastructure.Identity;

/// <summary>Why an external sign-in did not happen. Never shown to the user — they get one generic message.</summary>
public enum ExternalSignInFailure
{
    None = 0,
    NoPrincipal = 1,
    EmailMissing = 2,
    EmailUnverified = 3,
    NoMatchingUser = 4,
    UserInactive = 5,
    LinkFailed = 6,
}

public sealed record ExternalSignInOutcome(bool Succeeded, ExternalSignInFailure Failure);

/// <summary>
/// Signs somebody in with a Google account **that already belongs to a user here**.
///
/// No auto-provisioning: admin-created users is the rule v1 set, and an external provider is a way to
/// authenticate, not an invitation to join. The claim that matters is <c>email_verified</c> — without
/// it, "the e-mail matches" only means somebody typed it into a Google profile.
/// </summary>
public sealed class ExternalLoginService(
    EverdueDbContext db,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    ILogger<ExternalLoginService> logger)
{
    public const string ExternalClaimType = "everdue:external";

    public async Task<ExternalSignInOutcome> SignInAsync(ExternalLoginInfo info, CancellationToken cancellationToken = default)
    {
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return Fail(ExternalSignInFailure.EmailMissing, email);
        }

        // Google emits this as the string "true"/"false".
        var verified = info.Principal.FindFirstValue("email_verified");
        if (!string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(ExternalSignInFailure.EmailUnverified, email);
        }

        var normalized = userManager.NormalizeEmail(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);

        if (user is null)
        {
            return Fail(ExternalSignInFailure.NoMatchingUser, email);
        }

        if (!user.Active)
        {
            return Fail(ExternalSignInFailure.UserInactive, email);
        }

        // Linked on first use, so the provider key — not the address — identifies them from then on.
        var existingLogins = await userManager.GetLoginsAsync(user);
        if (!existingLogins.Any(l => l.LoginProvider == info.LoginProvider && l.ProviderKey == info.ProviderKey))
        {
            var result = await userManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "Could not link {Provider} login to {Email}: {Errors}",
                    info.LoginProvider,
                    email,
                    string.Join("; ", result.Errors.Select(e => e.Description)));

                return new ExternalSignInOutcome(false, ExternalSignInFailure.LinkFailed);
            }
        }

        // The external marker rides on the cookie so the forced-password-change gate can let this
        // session through: asking somebody to change a password they have never seen is a dead end.
        // The flag itself stays set, so the temporary password remains unusable.
        await signInManager.SignInWithClaimsAsync(
            user,
            isPersistent: true,
            [new Claim(ExternalClaimType, info.LoginProvider)]);

        logger.LogInformation("Signed in {Email} through {Provider}.", email, info.LoginProvider);
        return new ExternalSignInOutcome(true, ExternalSignInFailure.None);
    }

    /// <summary>
    /// Every refusal looks the same to the caller. Which account exists, which is deactivated and
    /// which never had a verified address are all things an unauthenticated visitor should not learn.
    /// </summary>
    private ExternalSignInOutcome Fail(ExternalSignInFailure failure, string? email)
    {
        logger.LogInformation("External sign-in refused ({Failure}) for {Email}.", failure, email ?? "unknown");
        return new ExternalSignInOutcome(false, failure);
    }
}
