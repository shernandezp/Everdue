using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Entities;

/// <summary><c>Type</c> is bound as a string so <c>?type=customer</c> works as well as <c>?type=Customer</c>.</summary>
public sealed record ListEntitiesQuery(
    string? Search = null,
    string? Type = null,
    bool IncludeInactive = false,
    int? Page = null,
    int? PageSize = null) : IQuery<PagedResult<EntityDto>>
{
    public EntityType? ResolvedType => EnumQuery.Parse<EntityType>(Type, nameof(Type));
}

public sealed record GetEntityQuery(Guid Id) : IQuery<EntityDto>;

/// <summary>
/// <paramref name="CustomFields"/> is keyed by <c>EntityFieldDef.Id</c>. Omitting it entirely leaves
/// stored values alone, so a client that knows nothing about custom fields cannot erase them; sending a
/// key with a null value clears that one field.
/// </summary>
public sealed record CreateEntityCommand(
    [property: Required, MaxLength(200)] string Name,
    EntityType Type,
    IReadOnlyDictionary<Guid, string?>? CustomFields = null) : ICommand<EntityDto>;

public sealed record UpdateEntityCommand(
    Guid Id,
    [property: Required, MaxLength(200)] string Name,
    EntityType Type,
    bool Active,
    IReadOnlyDictionary<Guid, string?>? CustomFields = null) : ICommand<EntityDto>;

/// <summary>DELETE is a soft deactivate: the ledger keeps pointing at the row forever.</summary>
public sealed record DeactivateEntityCommand(Guid Id) : ICommand<EntityDto>;
