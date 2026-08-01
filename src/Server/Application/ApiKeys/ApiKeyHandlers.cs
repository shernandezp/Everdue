using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.ApiKeys;

internal static class ApiKeyMapping
{
    public static ApiKeyDto ToDto(ApiKey key, IReadOnlyDictionary<Guid, UserSummary> actors)
        => new(
            key.Id,
            key.Name,
            key.KeyPrefix,
            key.Scope,
            key.ActorUserId,
            actors.TryGetValue(key.ActorUserId, out var actor) ? actor.DisplayName : "—",
            key.CreatedAt,
            key.ExpiresAt,
            key.LastUsedAt,
            key.RevokedAt);
}

public sealed class ListApiKeysHandler(IEverdueDbContext db, IUserDirectory users)
    : IRequestHandler<ListApiKeysQuery, IReadOnlyList<ApiKeyDto>>
{
    public async Task<IReadOnlyList<ApiKeyDto>> Handle(ListApiKeysQuery request, CancellationToken cancellationToken = default)
    {
        var query = db.ApiKeys.AsNoTracking();

        if (!request.IncludeRevoked)
        {
            query = query.Where(k => k.RevokedAt == null);
        }

        var keys = await query.OrderByDescending(k => k.CreatedAt).ToListAsync(cancellationToken);
        var actors = await users.MapAsync(keys.Select(k => k.ActorUserId), cancellationToken);

        return keys.Select(key => ApiKeyMapping.ToDto(key, actors)).ToArray();
    }
}

public sealed class CreateApiKeyHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<CreateApiKeyCommand, CreatedApiKeyDto>
{
    public async Task<CreatedApiKeyDto> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken = default)
    {
        var creator = currentUser.RequireUserId();
        var actorId = request.ActorUserId ?? creator;

        // The actor has to be somebody who could own work: a key acting as a deactivated user would write
        // ledger entries attributed to somebody who no longer has access.
        await users.RequireAssignableAsync(actorId, cancellationToken);

        if (request.ExpiresAt is { } expires && expires <= clock.UtcNow)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["expiresAt"] = ["An expiry date must be in the future."],
            });
        }

        var minted = ApiKeyToken.Create();

        var key = new ApiKey
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name.Trim(),
            KeyPrefix = minted.Prefix,
            KeyHash = minted.Hash,
            Scope = request.Scope,
            ActorUserId = actorId,
            CreatedByUserId = creator,
            CreatedAt = clock.UtcNow,
            ExpiresAt = request.ExpiresAt,
        };

        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(cancellationToken);

        var actors = await users.MapAsync([actorId], cancellationToken);

        // The only time the token exists outside the caller's hands.
        return new CreatedApiKeyDto(ApiKeyMapping.ToDto(key, actors), minted.Token);
    }
}

public sealed class RevokeApiKeyHandler(IEverdueDbContext db, IUserDirectory users, IClock clock)
    : IRequestHandler<RevokeApiKeyCommand, ApiKeyDto>
{
    public async Task<ApiKeyDto> Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken = default)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException(ResourceNames.ApiKey, request.Id);

        // Revoked rather than deleted: "this key existed and was withdrawn on the 3rd" is the answer somebody
        // needs during an incident, and a deleted row cannot give it.
        key.RevokedAt ??= clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var actors = await users.MapAsync([key.ActorUserId], cancellationToken);
        return ApiKeyMapping.ToDto(key, actors);
    }
}
