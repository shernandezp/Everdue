using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Tenants;

public sealed class GetTenantSettingsHandler(ITenantProvider tenants) : IRequestHandler<GetTenantSettingsQuery, TenantSettingsDto>
{
    public async Task<TenantSettingsDto> Handle(GetTenantSettingsQuery request, CancellationToken cancellationToken = default)
    {
        var tenant = await tenants.GetAsync(cancellationToken);
        return TenantSettingsMapping.Map(tenant);
    }
}

public sealed class UpdateTenantSettingsHandler(IEverdueDbContext db, ITenantContext tenantContext)
    : IRequestHandler<UpdateTenantSettingsCommand, TenantSettingsDto>
{
    public async Task<TenantSettingsDto> Handle(UpdateTenantSettingsCommand request, CancellationToken cancellationToken = default)
    {
        if (!TimeZoneLookup.IsKnown(request.TimeZoneId))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["timeZoneId"] = [$"'{request.TimeZoneId}' is not a time zone this machine knows. Use an IANA id such as 'America/Bogota'."],
            });
        }

        if (!Languages.IsSupported(request.DefaultLanguage))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["defaultLanguage"] = [$"Supported languages are: {string.Join(", ", Languages.Supported)}."],
            });
        }

        var id = tenantContext.TenantId;
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                     ?? throw new NotFoundException(ResourceNames.Tenant, id);

        tenant.Name = request.Name.Trim();
        tenant.TimeZoneId = request.TimeZoneId;
        tenant.DigestHourLocal = request.DigestHourLocal;
        tenant.DefaultLanguage = Languages.Normalize(request.DefaultLanguage);
        tenant.ReminderHourLocal = request.ReminderHourLocal;
        tenant.CanUseSystemChannels = request.CanUseSystemChannels;

        await db.SaveChangesAsync(cancellationToken);

        return TenantSettingsMapping.Map(tenant);
    }
}

/// <summary>
/// The single mapping from the tenant row to its DTO. Every caller goes through it — see the comment in
/// <c>CurrentUserBuilder</c> for what the second copy cost.
/// </summary>
internal static class TenantSettingsMapping
{
    public static TenantSettingsDto Map(Tenant tenant)
        => new(
            tenant.Id,
            tenant.Name,
            tenant.TimeZoneId,
            tenant.DigestHourLocal,
            tenant.DefaultLanguage,
            tenant.ReminderHourLocal,
            tenant.CanUseSystemChannels,
            tenant.DemoMode);
}
