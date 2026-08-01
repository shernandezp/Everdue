using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Infrastructure.Tenancy;

/// <summary>
/// Current resolution: the single configured tenant, fixed once at startup. Registered as a singleton
/// so every scope — request, engine tick, digest run — sees the same id.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private readonly Lock _gate = new();
    private Guid _tenantId;

    public Guid TenantId
    {
        get
        {
            lock (_gate)
            {
                return _tenantId;
            }
        }
    }

    public bool IsResolved => TenantId != Guid.Empty;

    public void Resolve(Guid tenantId)
    {
        lock (_gate)
        {
            _tenantId = tenantId;
        }
    }
}

/// <summary>Loads the resolved tenant row once per scope, and its <see cref="TimeZoneInfo"/> with it.</summary>
public sealed class TenantProvider(EverdueDbContext db, ITenantContext tenantContext) : ITenantProvider
{
    private Tenant? _cached;
    private TimeZoneInfo? _timeZone;

    public async Task<Tenant> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var id = tenantContext.TenantId;
        _cached = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException(
                      $"Tenant '{id}' is not present in the database. The instance has not been initialized.");

        return _cached;
    }

    public async Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default)
        => _timeZone ??= (await GetAsync(cancellationToken)).ResolveTimeZone();
}
