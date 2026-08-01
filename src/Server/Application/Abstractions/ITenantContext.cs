using Everdue.Server.Domain;

namespace Everdue.Server.Application.Abstractions;

/// <summary>
/// v1 tenant resolution: one configured tenant for the whole instance. The interface exists so the
/// hosted version can swap in per-request resolution without touching a single query.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }

    bool IsResolved { get; }

    void Resolve(Guid tenantId);
}

/// <summary>Loads the current tenant row and its time zone (cached for the lifetime of a scope).</summary>
public interface ITenantProvider
{
    Task<Tenant> GetAsync(CancellationToken cancellationToken = default);

    Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default);
}
