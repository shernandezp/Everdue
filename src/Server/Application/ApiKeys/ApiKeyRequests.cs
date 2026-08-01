using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.ApiKeys;

/// <summary>
/// What the admin list shows. Never the secret — there is no field for it, because a DTO that could carry one
/// is a DTO somebody eventually returns.
/// </summary>
public sealed record ApiKeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    ApiKeyScope Scope,
    Guid ActorUserId,
    string ActorDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);

/// <summary>
/// The one response that carries the token, returned by creation only. Everdue cannot show it again — only
/// the prefix and the hash are stored — and the screen says so beside the copy button.
/// </summary>
public sealed record CreatedApiKeyDto(ApiKeyDto Key, string Token);

public sealed record ListApiKeysQuery(bool IncludeRevoked = false) : IQuery<IReadOnlyList<ApiKeyDto>>;

/// <summary>
/// <paramref name="ActorUserId"/> is whose name the key's writes are recorded under; it defaults to the
/// administrator creating it. The ledger's "who did this" must never be null, and a key is not a person.
/// </summary>
public sealed record CreateApiKeyCommand(
    [property: Required, MaxLength(100)] string Name,
    ApiKeyScope Scope,
    Guid? ActorUserId = null,
    DateTimeOffset? ExpiresAt = null) : ICommand<CreatedApiKeyDto>;

/// <summary>Revocation is immediate: the store reads <c>RevokedAt</c> on every authentication.</summary>
public sealed record RevokeApiKeyCommand(Guid Id) : ICommand<ApiKeyDto>;
