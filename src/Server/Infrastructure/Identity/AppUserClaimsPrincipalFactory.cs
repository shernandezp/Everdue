using System.Security.Claims;
using Everdue.Server.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Infrastructure.Identity;

/// <summary>
/// Projects the custom user columns into the auth cookie so authorization and the SPA bootstrap
/// need no extra round trip. Role is a column on the user, not an Identity role table.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<AppUser>(userManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        identity.AddClaim(new Claim(CurrentUser.DisplayNameClaim, user.DisplayName));
        identity.AddClaim(new Claim(CurrentUser.LanguageClaim, user.PreferredLanguage ?? string.Empty));

        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(CurrentUser.MustChangePasswordClaim, "true"));
        }

        return identity;
    }
}
