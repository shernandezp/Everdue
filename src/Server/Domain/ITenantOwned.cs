namespace Everdue.Server.Domain;

/// <summary>
/// Marker for every tenant-owned table. A single EF Core global query filter is applied by
/// convention to every entity implementing this, so isolation lives in one place instead of
/// being re-stated per query (see Infrastructure/Persistence/EverdueDbContext).
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
