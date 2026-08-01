using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Everdue.Server.Application.Demo;

public sealed class GetDemoStatusHandler(ITenantProvider tenants, IDemoMode demo)
    : IRequestHandler<GetDemoStatusQuery, DemoStatusDto>
{
    public async Task<DemoStatusDto> Handle(GetDemoStatusQuery request, CancellationToken cancellationToken = default)
    {
        var tenant = await tenants.GetAsync(cancellationToken);

        return new DemoStatusDto(tenant.DemoMode, demo.ResetAllowed, tenant.Name, demo.DemoPassword);
    }
}

/// <summary>
/// The only place in Everdue that destroys the ledger.
///
/// <para>Everything else in this codebase exists to make a recorded miss impossible to erase — no status, no
/// edit and no report may take one down. That rule is about the *product*: it stops the software quietly
/// flattering its users. It was never meant to stop an owner from clearing their own install, and pretending
/// otherwise would only mean they did it with <c>rm</c> instead, with no audit line and no way back to a
/// working database.</para>
///
/// <para>So this handler is the deliberate, single, loudly-guarded exception, and its guards are the reason
/// it is allowed to exist at all: administrator, cookie session (never an API key), the tenant's name typed
/// out, and the caller's own password. Anything less and the exception would become a hole.</para>
/// </summary>
public sealed class DemoModeHandler(
    IEverdueDbContext db,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IAuthService auth,
    IDemoMode demo,
    ILogger<DemoModeHandler> logger) : IRequestHandler<DemoModeCommand, DemoModeResultDto>
{
    public async Task<DemoModeResultDto> Handle(DemoModeCommand request, CancellationToken cancellationToken = default)
    {
        // 404 rather than 403: an install with Demo:AllowReset off does not have this feature, and saying
        // "forbidden" would tell a caller that the right credentials exist somewhere.
        if (!demo.ResetAllowed)
        {
            throw new NotFoundException(ResourceNames.DemoMode, "reset");
        }

        // ApiKeyGate already refuses this endpoint to a key, because the endpoint is not marked AllowApiKey.
        // Stated again here because it is a rule about the operation and not about routing: no script wipes a
        // tenant, however privileged the person behind the key.
        if (currentUser.ApiKeyId is not null)
        {
            throw new ForbiddenException("Demo mode cannot be changed with an API key. Sign in and confirm from the settings screen.");
        }

        var userId = currentUser.RequireUserId();

        var id = tenantContext.TenantId;
        var name = await db.Tenants.Where(t => t.Id == id).Select(t => t.Name).FirstOrDefaultAsync(cancellationToken)
                   ?? throw new NotFoundException(ResourceNames.Tenant, id);

        // Field-level 400s rather than a 401 or a 409: both of these are the administrator getting a form
        // wrong, and both belong beside the box they typed into. A 401 in particular would read to the client
        // as an expired session and bounce them to the login screen mid-confirmation.
        if (!string.Equals(request.Confirmation.Trim(), name.Trim(), StringComparison.Ordinal))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["confirmation"] = [$"Type the workspace name exactly — '{name}' — to confirm."],
            });
        }

        if (!await auth.VerifyPasswordAsync(userId, request.Password, cancellationToken))
        {
            // Logged because a wrong password here is worth seeing in a log even though it is not, on its own,
            // an attack: the caller already holds an administrator's cookie and could reset any password with
            // it. This gate proves the person is at the keyboard, not that the session is trustworthy.
            logger.LogWarning("Rejected a demo-mode change by user {UserId}: the password did not verify.", userId);

            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["password"] = ["That is not your password."],
            });
        }

        logger.LogWarning(
            "User {UserId} is turning demo mode {State} for tenant {TenantId}. The tenant is about to be wiped.",
            userId,
            request.Enabled ? "ON" : "OFF",
            id);

        var deleted = await demo.WipeAsync(userId, cancellationToken);

        // Re-read: the wipe detaches everything the request had loaded (see TenantWipe). The tenant row itself
        // survives the wipe — it is the one thing that has to.
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                     ?? throw new NotFoundException(ResourceNames.Tenant, id);

        // Flagged before seeding, not after. If the seed then fails, an install marked "demo" and left empty
        // is a state an administrator can fix by pressing the button again; an install holding demo data but
        // insisting it is real is one nobody would think to. The seeder sets the same flag itself, for the
        // startup path that never comes through here — both roads lead to "demo data implies the badge".
        tenant.DemoMode = request.Enabled;
        await db.SaveChangesAsync(cancellationToken);

        var seeded = request.Enabled ? await demo.SeedAsync(cancellationToken) : null;

        return new DemoModeResultDto(
            new DemoStatusDto(request.Enabled, demo.ResetAllowed, tenant.Name, demo.DemoPassword),
            new DemoWipeCountsDto(
                deleted.WorkItems,
                deleted.Responsibilities,
                deleted.Entities,
                deleted.Departments,
                deleted.Users,
                deleted.Attachments,
                deleted.Notifications),
            seeded is null
                ? null
                : new DemoSeedCountsDto(
                    seeded.Users,
                    seeded.Entities,
                    seeded.Responsibilities,
                    seeded.Occurrences,
                    seeded.Tasks,
                    seeded.Password));
    }
}
