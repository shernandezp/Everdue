using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Infrastructure.Identity;

/// <summary>
/// Cookie sign-in. No tokens, no CORS, no CSRF machinery: the SPA is same-origin and the cookie is
/// HttpOnly + SameSite=Strict (see ApiAuthenticationExtensions).
/// </summary>
public sealed class AuthService(
    EverdueDbContext db,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager) : IAuthService
{
    public async Task<SignInOutcome> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == userManager.NormalizeEmail(email), cancellationToken);

        // Same answer for "no such user", "wrong password" and "deactivated": never enumerate accounts.
        if (user is null || !user.Active)
        {
            return new SignInOutcome(false, "invalid_credentials", null);
        }

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return new SignInOutcome(false, "locked_out", null);
        }

        return result.Succeeded
            ? new SignInOutcome(true, null, Summarize(user))
            : new SignInOutcome(false, "invalid_credentials", null);
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default) => signInManager.SignOutAsync();

    public async Task ChangeOwnPasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await Require(userId, cancellationToken);

        // Identity does not check this, so without it the forced first-run change could be satisfied
        // by retyping the seeded password — which leaves the instance exactly as exposed as before.
        if (await userManager.CheckPasswordAsync(user, newPassword))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["newPassword"] = ["The new password must be different from the current one."],
            });
        }

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["password"] = result.Errors.Select(e => e.Description).ToArray(),
            });
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);
    }

    public async Task RefreshSignInAsync(Guid userId, CancellationToken cancellationToken = default)
        => await signInManager.RefreshSignInAsync(await Require(userId, cancellationToken));

    public async Task<bool> VerifyPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user is not null && await userManager.CheckPasswordAsync(user, password);
    }

    public async Task UpdateOwnProfileAsync(Guid userId, string displayName, string? preferredLanguage, CancellationToken cancellationToken = default)
    {
        var user = await Require(userId, cancellationToken);

        user.DisplayName = displayName;
        user.PreferredLanguage = Languages.NormalizeOptional(preferredLanguage);

        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);
    }

    private async Task<AppUser> Require(Guid userId, CancellationToken cancellationToken)
        => await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
           ?? throw new NotFoundException(ResourceNames.User, userId);

    private static UserSummary Summarize(AppUser u)
        => new(u.Id, u.Email ?? string.Empty, u.DisplayName, u.Role, u.PreferredLanguage, u.Active, u.MustChangePassword);
}
