using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Application.Contracts;

namespace Everdue.Server.Application.Auth;

public sealed record LoginCommand(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password) : ICommand<CurrentUserDto>;

public sealed record LogoutCommand : ICommand<bool>;

public sealed record MeQuery : IQuery<CurrentUserDto>;

public sealed record ChangeOwnPasswordCommand(
    [property: Required] string CurrentPassword,
    [property: Required, MinLength(10), MaxLength(128)] string NewPassword) : ICommand<bool>;

public sealed record UpdateOwnProfileCommand(
    [property: Required, MaxLength(200)] string DisplayName,
    string? PreferredLanguage) : ICommand<CurrentUserDto>;
