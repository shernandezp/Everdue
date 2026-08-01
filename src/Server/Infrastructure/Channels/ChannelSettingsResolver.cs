using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Everdue.Server.Infrastructure.Channels;

/// <summary>
/// The only reader of the ChannelSettings table — which is what makes it safe for that table to sit
/// outside the global tenant filter. Every query here names its scope explicitly.
/// </summary>
public sealed class ChannelSettingsResolver(
    EverdueDbContext db,
    ITenantContext tenantContext,
    ChannelSecretProtector protector,
    IClock clock,
    ILogger<ChannelSettingsResolver> logger) : IChannelSettingsResolver
{
    public async Task<ResolvedChannelConfig?> ResolveAsync(
        NotificationChannel channel,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId;

        if (await ReadScopeAsync(tenantId, channel, cancellationToken) is { } tenantConfig)
        {
            return tenantConfig;
        }

        // The system's credentials are a privilege, not a default: a hosted free plan turns the flag
        // off so a tenant must bring its own. Self-host leaves it on, where "system" and "tenant" are
        // the same operator anyway.
        if (!await CanUseSystemChannelsAsync(cancellationToken))
        {
            return null;
        }

        // WhatsApp is never shared. The sender identity is a specific business's, and lending it to
        // another company's staff messages is the tech-provider model — a commercial relationship,
        // not a configuration flag.
        if (channel == NotificationChannel.WhatsApp)
        {
            return null;
        }

        return await ReadScopeAsync(ChannelSettings.SystemScope, channel, cancellationToken);
    }

    public async Task<bool> CanUseSystemChannelsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId;
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        return tenant?.CanUseSystemChannels == true;
    }

    public async Task<ResolvedChannelConfig?> ReadScopeAsync(
        Guid scope,
        NotificationChannel channel,
        CancellationToken cancellationToken = default)
    {
        var row = await db.ChannelSettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == scope && c.Channel == channel, cancellationToken);

        if (row is null || !row.Active)
        {
            return null;
        }

        var json = protector.TryUnprotect(row.ConfigProtected);
        if (json is null)
        {
            logger.LogWarning(
                "Channel {Channel} at scope {Scope} could not be decrypted; treating it as unconfigured. " +
                "The data-protection key ring in the data directory has probably been lost or replaced.",
                channel,
                scope);
            return null;
        }

        return new ResolvedChannelConfig(channel, scope, json);
    }

    public async Task SaveAsync(
        Guid scope,
        NotificationChannel channel,
        string configJson,
        bool active,
        CancellationToken cancellationToken = default)
    {
        var row = await db.ChannelSettings
            .FirstOrDefaultAsync(c => c.TenantId == scope && c.Channel == channel, cancellationToken);

        if (row is null)
        {
            row = new ChannelSettings
            {
                Id = Guid.CreateVersion7(),
                TenantId = scope,
                Channel = channel,
            };

            db.ChannelSettings.Add(row);
        }

        row.ConfigProtected = protector.Protect(configJson);
        row.Active = active;
        row.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid scope, NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        var row = await db.ChannelSettings
            .FirstOrDefaultAsync(c => c.TenantId == scope && c.Channel == channel, cancellationToken);

        if (row is null)
        {
            return;
        }

        db.ChannelSettings.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
    }
}
