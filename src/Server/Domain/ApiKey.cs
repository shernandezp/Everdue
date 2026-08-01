namespace Everdue.Server.Domain;

/// <summary>
/// A credential for the public API. Stored as a prefix plus a hash — the secret itself is shown once,
/// at creation, and is unrecoverable afterwards.
///
/// Two decisions are worth reading here. First, <see cref="ActorUserId"/>: every write a key makes is
/// attributed to a real person, because the ledger's "who did this" must never become null. Second,
/// what a key may reach is an <em>endpoint allow-list</em> (see <c>ApiKeyGate</c>), not the actor's
/// role — so a leaked key cannot create a user or read a channel secret even when the person who
/// created it is an administrator.
/// </summary>
public class ApiKey : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    /// <summary>What it is for, in a human's words. Shown in the admin list beside the prefix.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The public part of the token, used to find the row. Indexed but <strong>not unique</strong>:
    /// with a 256-bit secret a prefix collision is a curiosity, and a unique index would turn it into
    /// a failed key creation. The store verifies the hash against every prefix match.
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>Base64 SHA-256 of the secret. See <see cref="ApiKeyToken"/> for why SHA-256 is right here.</summary>
    public string KeyHash { get; set; } = string.Empty;

    public ApiKeyScope Scope { get; set; } = ApiKeyScope.ReadOnly;

    /// <summary>Whose name the key's writes are recorded under. Defaults to its creator.</summary>
    public Guid ActorUserId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Stamped at most once every few minutes per key, not on every request: "when was this last used"
    /// is worth one write per interval and not one per call.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsUsableAt(DateTimeOffset now)
        => RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);

    /// <summary>Read-only means read-only: every method that is not a safe read is refused.</summary>
    public bool Allows(string httpMethod)
        => Scope == ApiKeyScope.ReadWrite
           || string.Equals(httpMethod, "GET", StringComparison.OrdinalIgnoreCase)
           || string.Equals(httpMethod, "HEAD", StringComparison.OrdinalIgnoreCase);
}
