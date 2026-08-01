using System.Linq.Expressions;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Infrastructure.Identity;

/// <summary>
/// Read side of the user table. Every query rides the tenant filter on <see cref="AppUser"/>,
/// so a foreign user is invisible here in exactly the same way a foreign work item is.
/// </summary>
public sealed class UserDirectory(EverdueDbContext db) : IUserDirectory
{
    /// <summary>Kept as an expression tree so EF translates it into the SELECT list instead of materializing rows.</summary>
    private static readonly Expression<Func<AppUser, UserSummary>> Projection =
        u => new UserSummary(
            u.Id,
            u.Email ?? string.Empty,
            u.DisplayName,
            u.Role,
            u.PreferredLanguage,
            u.Active,
            u.MustChangePassword,
            u.WhatsAppPhoneE164);

    public async Task<IReadOnlyList<UserSummary>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
        => await db.Users
            .AsNoTracking()
            .Where(u => includeInactive || u.Active)
            .OrderBy(u => u.DisplayName)
            .Select(Projection)
            .ToListAsync(cancellationToken);

    public async Task<UserSummary?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        => await db.Users.AsNoTracking().Where(u => u.Id == id).Select(Projection).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, UserSummary>> MapAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var distinct = ids.Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return new Dictionary<Guid, UserSummary>();
        }

        var users = await db.Users
            .AsNoTracking()
            .Where(u => distinct.Contains(u.Id))
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Id);
    }

    public async Task<UserSummary> RequireAssignableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(id, cancellationToken)
                   ?? throw new NotFoundException(ResourceNames.User, id);

        return user.Active
            ? user
            : throw new ValidationException($"User '{user.DisplayName}' is deactivated and cannot be assigned new work.");
    }
}

public sealed class UserAdmin(
    EverdueDbContext db,
    UserManager<AppUser> userManager,
    IClock clock,
    ITenantContext tenantContext) : IUserAdmin
{
    public async Task<UserSummary> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email, cancellationToken))
        {
            throw new ValidationException($"A user with e-mail '{request.Email}' already exists.");
        }

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantContext.TenantId,
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            DisplayName = request.DisplayName,
            Role = request.Role,
            PreferredLanguage = Languages.NormalizeOptional(request.PreferredLanguage),
            Active = true,
            MustChangePassword = true,
            CreatedAt = clock.UtcNow,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        Ensure(result);

        return Summarize(user);
    }

    public async Task<UserSummary> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
                   ?? throw new NotFoundException(ResourceNames.User, id);

        // Losing access and losing a role both have to reach an already-signed-in browser. Rotating
        // the security stamp is what invalidates the cookie it is already holding; the validation
        // interval (see AddEverdueIdentity) bounds how long the old one keeps working.
        var accessChanged = user.Active != request.Active || user.Role != request.Role;

        user.DisplayName = request.DisplayName;
        user.Role = request.Role;
        user.PreferredLanguage = Languages.NormalizeOptional(request.PreferredLanguage);
        user.Active = request.Active;
        user.WhatsAppPhoneE164 = NormalizePhone(request.WhatsAppPhoneE164);

        Ensure(await userManager.UpdateAsync(user));

        if (accessChanged)
        {
            Ensure(await userManager.UpdateSecurityStampAsync(user));
        }

        return Summarize(user);
    }

    public async Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
                   ?? throw new NotFoundException(ResourceNames.User, id);

        // Same rule as a self-service change: "reset" has to actually change something.
        if (await userManager.CheckPasswordAsync(user, newPassword))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["newPassword"] = ["The new password must be different from the current one."],
            });
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        Ensure(await userManager.ResetPasswordAsync(user, token, newPassword));

        user.MustChangePassword = true;
        Ensure(await userManager.UpdateAsync(user));
    }

    private static UserSummary Summarize(AppUser u)
        => new(
            u.Id,
            u.Email ?? string.Empty,
            u.DisplayName,
            u.Role,
            u.PreferredLanguage,
            u.Active,
            u.MustChangePassword,
            u.WhatsAppPhoneE164);

    /// <summary>
    /// E.164 or nothing: <c>+</c>, a country code that is not zero, then up to fourteen more digits.
    /// Checked here because a number that Meta will reject is worth catching at the moment somebody
    /// types it, not on the first missed occurrence.
    /// </summary>
    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);

        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\+[1-9]\d{6,14}$"))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["whatsAppPhoneE164"] = ["Use international format, for example +573001112233."],
            });
        }

        return trimmed;
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["identity"] = result.Errors.Select(e => e.Description).ToArray(),
            });
        }
    }
}
