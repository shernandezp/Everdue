using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Infrastructure.ApiKeys;

/// <summary>
/// Finds and verifies a presented key.
///
/// <para><strong>Why this reads with the tenant filter ignored:</strong> authentication runs before anything
/// knows which tenant is being served — the key is what decides. This is the only place the table is read
/// that way, and it is the same exemption channel settings documents for the same kind of reason.</para>
///
/// <para><strong>Why <c>LastUsedAt</c> is throttled:</strong> "when was this key last used" is worth one write
/// every few minutes. One write per request would put a database round trip on the hot path of every
/// authenticated API call to record something nobody reads to the second.</para>
/// </summary>
public sealed class ApiKeyStore(EverdueDbContext db, IClock clock, ILogger<ApiKeyStore> logger) : IApiKeyStore
{
    private static readonly TimeSpan LastUsedThrottle = TimeSpan.FromMinutes(5);

    /// <summary>Process-wide, so the throttle survives the scoped lifetime of this class.</summary>
    private static readonly Dictionary<Guid, DateTimeOffset> LastStamped = [];

    private static readonly Lock StampGate = new();

    public async Task<ApiKeyPrincipalInfo?> AuthenticateAsync(string presentedToken, CancellationToken cancellationToken)
    {
        if (!ApiKeyToken.TryParse(presentedToken, out var prefix, out var secret))
        {
            return null;
        }

        var now = clock.UtcNow;

        // Every row sharing the prefix. The prefix index is not unique on purpose — with a 256-bit secret a
        // collision is a curiosity, and a unique index would turn it into a failed key creation instead.
        var candidates = await db.ApiKeys
            .IgnoreQueryFilters()
            .Where(k => k.KeyPrefix == prefix)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            if (!ApiKeyToken.Matches(candidate.KeyHash, secret))
            {
                continue;
            }

            if (!candidate.IsUsableAt(now))
            {
                logger.LogDebug("API key {KeyId} was presented but is revoked or expired.", candidate.Id);
                return null;
            }

            await StampLastUsedAsync(candidate, now, cancellationToken);

            return new ApiKeyPrincipalInfo(
                candidate.Id,
                candidate.TenantId,
                candidate.ActorUserId,
                candidate.Scope,
                candidate.Name);
        }

        return null;
    }

    private async Task StampLastUsedAsync(ApiKey key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (StampGate)
        {
            if (LastStamped.TryGetValue(key.Id, out var last) && now - last < LastUsedThrottle)
            {
                return;
            }

            LastStamped[key.Id] = now;
        }

        try
        {
            key.LastUsedAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            // A bookkeeping column must never be able to fail a request that was otherwise authenticated.
            logger.LogWarning(e, "Could not stamp LastUsedAt on API key {KeyId}.", key.Id);
            db.ChangeTracker.Clear();
        }
    }
}
