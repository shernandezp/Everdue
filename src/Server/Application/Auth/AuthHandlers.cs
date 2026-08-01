using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Tenants;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Auth;

internal sealed class CurrentUserBuilder(ITenantProvider tenants, IUserDirectory users)
{
    public async Task<CurrentUserDto> BuildAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(userId, cancellationToken) ?? throw new NotFoundException(ResourceNames.User, userId);
        return await BuildAsync(user, cancellationToken);
    }

    public async Task<CurrentUserDto> BuildAsync(UserSummary user, CancellationToken cancellationToken)
    {
        var tenant = await tenants.GetAsync(cancellationToken);

        return new CurrentUserDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            Languages.Resolve(user.PreferredLanguage, tenant.DefaultLanguage),
            user.MustChangePassword,

            // Through the shared mapping, never a second constructor call. This built its own DTO once and
            // silently reported the *defaults* for every field added after it — the reminder hour, the shared-
            // channel flag, and then the demo-mode flag, which is read from exactly here to draw the "this is
            // not real data" badge. A tenant field that half the API lies about is worse than one that is missing.
            TenantSettingsMapping.Map(tenant));
    }
}

public sealed class LoginHandler(IAuthService auth, ITenantProvider tenants, IUserDirectory users)
    : IRequestHandler<LoginCommand, CurrentUserDto>
{
    public async Task<CurrentUserDto> Handle(LoginCommand request, CancellationToken cancellationToken = default)
    {
        var outcome = await auth.SignInAsync(request.Email.Trim(), request.Password, cancellationToken);

        if (!outcome.Succeeded || outcome.User is null)
        {
            throw new UnauthenticatedException(outcome.FailureReason switch
            {
                "locked_out" => "This account is temporarily locked after too many failed attempts.",
                _ => "E-mail or password is incorrect.",
            });
        }

        return await new CurrentUserBuilder(tenants, users).BuildAsync(outcome.User, cancellationToken);
    }
}

public sealed class LogoutHandler(IAuthService auth) : IRequestHandler<LogoutCommand, bool>
{
    public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken = default)
    {
        await auth.SignOutAsync(cancellationToken);
        return true;
    }
}

public sealed class MeHandler(ICurrentUser currentUser, ITenantProvider tenants, IUserDirectory users)
    : IRequestHandler<MeQuery, CurrentUserDto>
{
    public Task<CurrentUserDto> Handle(MeQuery request, CancellationToken cancellationToken = default)
        => new CurrentUserBuilder(tenants, users).BuildAsync(currentUser.RequireUserId(), cancellationToken);
}

public sealed class ChangeOwnPasswordHandler(IAuthService auth, ICurrentUser currentUser)
    : IRequestHandler<ChangeOwnPasswordCommand, bool>
{
    public async Task<bool> Handle(ChangeOwnPasswordCommand request, CancellationToken cancellationToken = default)
    {
        await auth.ChangeOwnPasswordAsync(currentUser.RequireUserId(), request.CurrentPassword, request.NewPassword, cancellationToken);
        return true;
    }
}

public sealed class UpdateOwnProfileHandler(IAuthService auth, ICurrentUser currentUser, ITenantProvider tenants, IUserDirectory users)
    : IRequestHandler<UpdateOwnProfileCommand, CurrentUserDto>
{
    public async Task<CurrentUserDto> Handle(UpdateOwnProfileCommand request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        await auth.UpdateOwnProfileAsync(userId, request.DisplayName.Trim(), request.PreferredLanguage, cancellationToken);
        return await new CurrentUserBuilder(tenants, users).BuildAsync(userId, cancellationToken);
    }
}
