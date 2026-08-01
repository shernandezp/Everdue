using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Users;

internal static class UserMapping
{
    public static UserDto ToDto(UserSummary user)
        => new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.PreferredLanguage,
            user.Active,
            user.MustChangePassword,
            user.WhatsAppPhoneE164);
}

public sealed class ListUsersHandler(IUserDirectory users, ICurrentUser currentUser)
    : IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken = default)
    {
        // Members read this to pick an owner, so they get the assignable people and nothing more:
        // no deactivated colleagues and no view of who is mid-password-reset.
        var includeInactive = currentUser.IsAdmin && request.IncludeInactive;
        var found = await users.ListAsync(includeInactive, cancellationToken);

        // A member picking an owner needs names, not colleagues' phone numbers or who is mid-reset.
        return currentUser.IsAdmin
            ? found.Select(UserMapping.ToDto).ToArray()
            : found
                .Select(user => UserMapping.ToDto(user) with { MustChangePassword = false, WhatsAppPhoneE164 = null })
                .ToArray();
    }
}

public sealed class CreateUserHandler(IUserAdmin admin) : IRequestHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken = default)
    {
        var created = await admin.CreateAsync(
            new CreateUserRequest(request.Email.Trim(), request.Password, request.DisplayName.Trim(), request.Role, request.PreferredLanguage),
            cancellationToken);

        return UserMapping.ToDto(created);
    }
}

public sealed class UpdateUserHandler(IUserAdmin admin, ICurrentUser currentUser) : IRequestHandler<UpdateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken = default)
    {
        // An administrator locking themselves out of their own instance is a support call, not a feature.
        if (request.Id == currentUser.UserId && (!request.Active || request.Role != UserRole.Admin))
        {
            throw new ValidationException("You cannot remove your own administrator role or deactivate your own account.");
        }

        var updated = await admin.UpdateAsync(
            request.Id,
            new UpdateUserRequest(
                request.DisplayName.Trim(),
                request.Role,
                request.PreferredLanguage,
                request.Active,
                request.WhatsAppPhoneE164),
            cancellationToken);

        return UserMapping.ToDto(updated);
    }
}

public sealed class ResetUserPasswordHandler(IUserAdmin admin) : IRequestHandler<ResetUserPasswordCommand, bool>
{
    public async Task<bool> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken = default)
    {
        await admin.ResetPasswordAsync(request.Id, request.NewPassword, cancellationToken);
        return true;
    }
}
