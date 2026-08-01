using Everdue.Server.Domain;

namespace Everdue.Server.Application.Abstractions;

/// <summary>Decrypted configuration for one channel, plus which scope answered.</summary>
public sealed record ResolvedChannelConfig(NotificationChannel Channel, Guid Scope, string ConfigJson)
{
    public bool IsSystemScope => Scope == ChannelSettings.SystemScope;

    public T? Read<T>() where T : class => ChannelConfigJson.Deserialize<T>(ConfigJson);
}

/// <summary>
/// The resolution order, in one place: **tenant credentials → system credentials (only if the tenant
/// may use them)**. Nothing else reads the ChannelSettings table, which is what keeps the one table
/// outside the global tenant filter safe.
/// </summary>
public interface IChannelSettingsResolver
{
    Task<ResolvedChannelConfig?> ResolveAsync(NotificationChannel channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// May this tenant use credentials it did not provide? Exposed because e-mail has a second
    /// system-scope source the resolver does not own — v1's <c>Smtp:*</c> block — and that block is
    /// the operator's mail server just as much as a system row is. A flag that governed one and not
    /// the other would not be a flag.
    /// </summary>
    Task<bool> CanUseSystemChannelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads one scope's row verbatim, for the settings screen. Never returns secrets to callers that redact.</summary>
    Task<ResolvedChannelConfig?> ReadScopeAsync(Guid scope, NotificationChannel channel, CancellationToken cancellationToken = default);

    Task SaveAsync(Guid scope, NotificationChannel channel, string configJson, bool active, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid scope, NotificationChannel channel, CancellationToken cancellationToken = default);
}
