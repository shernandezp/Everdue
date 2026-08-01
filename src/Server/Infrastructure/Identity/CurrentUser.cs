using System.Security.Claims;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.ApiKeys;
using Microsoft.AspNetCore.Http;

namespace Everdue.Server.Infrastructure.Identity;

/// <summary>Reads the signed-in principal. Everything it exposes comes from cookie claims — no database hit per request.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string LanguageClaim = "everdue:lang";
    public const string DisplayNameClaim = "everdue:name";
    public const string MustChangePasswordClaim = "everdue:pwd_reset";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? DisplayName => Principal?.FindFirstValue(DisplayNameClaim);

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool IsAdmin => Role == UserRole.Admin;

    public Guid? ApiKeyId =>
        Guid.TryParse(Principal?.FindFirstValue(ApiKeyAuthenticationHandler.KeyIdClaim), out var id) ? id : null;

    public Guid RequireUserId() => UserId ?? throw new UnauthenticatedException();
}
