using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Users;

public sealed record ListUsersQuery(bool IncludeInactive = true) : IQuery<IReadOnlyList<UserDto>>;

/// <summary>There is no self-service registration in v1: an administrator creates every account.</summary>
public sealed record CreateUserCommand(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MinLength(10), MaxLength(128)] string Password,
    [property: Required, MaxLength(200)] string DisplayName,
    UserRole Role,
    string? PreferredLanguage) : ICommand<UserDto>;

/// <summary>
/// <paramref name="WhatsAppPhoneE164"/> is here rather than on the person's own profile because
/// WhatsApp has no linking flow — without a public webhook there is nothing for a user to confirm
/// against, so an administrator enters the number and takes responsibility for having asked first.
/// </summary>
public sealed record UpdateUserCommand(
    Guid Id,
    [property: Required, MaxLength(200)] string DisplayName,
    UserRole Role,
    string? PreferredLanguage,
    bool Active,
    [property: MaxLength(20)] string? WhatsAppPhoneE164 = null) : ICommand<UserDto>;

public sealed record ResetUserPasswordCommand(
    Guid Id,
    [property: Required, MinLength(10), MaxLength(128)] string NewPassword) : ICommand<bool>;
