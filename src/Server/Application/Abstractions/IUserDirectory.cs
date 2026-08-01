using Everdue.Server.Domain;

namespace Everdue.Server.Application.Abstractions;

/// <summary>The projection of a user the Application layer needs. ASP.NET Identity stays in Infrastructure.</summary>
public sealed record UserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    string? PreferredLanguage,
    bool Active,
    bool MustChangePassword,

    /// <summary>
    /// E.164, administrator-maintained. WhatsApp has no linking flow without a public webhook, so
    /// somebody has to type this in — which is also why it is the one contact detail an
    /// administrator edits on another person's behalf.
    /// </summary>
    string? WhatsAppPhoneE164 = null);

public interface IUserDirectory
{
    Task<IReadOnlyList<UserSummary>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<UserSummary?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Display names for a set of ids, for hydrating list and report DTOs in one round trip.</summary>
    Task<IReadOnlyDictionary<Guid, UserSummary>> MapAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Ownership assignment must fail loudly rather than point at a deactivated or foreign user.</summary>
    Task<UserSummary> RequireAssignableAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record CreateUserRequest(string Email, string Password, string DisplayName, UserRole Role, string? PreferredLanguage);

public sealed record UpdateUserRequest(
    string DisplayName,
    UserRole Role,
    string? PreferredLanguage,
    bool Active,
    string? WhatsAppPhoneE164 = null);

public interface IUserAdmin
{
    Task<UserSummary> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserSummary> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Admin-issued reset. Always forces a change on next login.</summary>
    Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default);
}

public sealed record SignInOutcome(bool Succeeded, string? FailureReason, UserSummary? User);

public interface IAuthService
{
    Task<SignInOutcome> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task ChangeOwnPasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Re-issues the auth cookie so claim changes (language, display name) take effect immediately.</summary>
    Task RefreshSignInAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Does <paramref name="password"/> belong to <paramref name="userId"/>? For the handful of actions where
    /// holding the cookie is not enough — an unattended laptop is a cookie, and some actions cannot be undone.
    /// Never signs anybody in, so it neither refreshes the session nor counts towards lockout.
    /// </summary>
    Task<bool> VerifyPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default);

    Task UpdateOwnProfileAsync(Guid userId, string displayName, string? preferredLanguage, CancellationToken cancellationToken = default);
}
