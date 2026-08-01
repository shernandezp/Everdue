using Everdue.Server.Domain;

namespace Everdue.Server.Application.Abstractions;

/// <summary>What authentication learned from a presented key.</summary>
public sealed record ApiKeyPrincipalInfo(Guid KeyId, Guid TenantId, Guid ActorUserId, ApiKeyScope Scope, string Name);

/// <summary>
/// Looking a key up at authentication time.
///
/// Its own abstraction, rather than a query in a handler, because it is the one read in the system that
/// happens <strong>before the tenant is known</strong> — the key is what resolves it. The implementation
/// therefore reads with the global tenant filter ignored, and it is the only place that does so for this
/// table. Same documented exemption pattern as channel settings.
/// </summary>
public interface IApiKeyStore
{
    /// <summary>
    /// Null for an unknown, revoked or expired key, and for a hash that does not match — a caller can never
    /// tell those apart, which is the point.
    /// </summary>
    Task<ApiKeyPrincipalInfo?> AuthenticateAsync(string presentedToken, CancellationToken cancellationToken);
}
