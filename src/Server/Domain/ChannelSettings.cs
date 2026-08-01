namespace Everdue.Server.Domain;

/// <summary>
/// Credentials for one channel, at one scope.
///
/// **Deliberately not <see cref="ITenantOwned"/>.** It is the only table exempt from the global
/// tenant filter, because a system-scope row has to be readable while serving a tenant. System scope
/// is <see cref="SystemScope"/> (<c>Guid.Empty</c>) rather than NULL: a nullable tenant column would
/// fall outside the filter *and* defeat the unique index, since NULLs are distinct on both
/// providers — two competing "system" rows for one channel is not a state worth being able to reach.
///
/// The exemption is why nothing reads this table directly except the resolver, which always filters
/// explicitly.
/// </summary>
public class ChannelSettings
{
    /// <summary>The tenant id that means "system scope": credentials shared by every tenant that may use them.</summary>
    public static readonly Guid SystemScope = Guid.Empty;

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public NotificationChannel Channel { get; set; }

    /// <summary>Data-Protection ciphertext of the channel's config object. Never leaves the server.</summary>
    public string ConfigProtected { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsSystemScope => TenantId == SystemScope;
}
