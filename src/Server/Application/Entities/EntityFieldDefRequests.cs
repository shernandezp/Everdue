using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Entities;

/// <summary><c>EntityType</c> is bound as a string so <c>?entityType=customer</c> works.</summary>
public sealed record ListEntityFieldDefsQuery(string? EntityType = null, bool IncludeInactive = false)
    : IQuery<IReadOnlyList<EntityFieldDefDto>>
{
    public EntityType? ResolvedEntityType => EnumQuery.Parse<EntityType>(EntityType, nameof(EntityType));
}

public sealed record CreateEntityFieldDefCommand(
    EntityType EntityType,
    [property: Required, MaxLength(50)] string Name,
    EntityFieldType FieldType,
    IReadOnlyList<string>? Options) : ICommand<EntityFieldDefDto>;

public sealed record UpdateEntityFieldDefCommand(
    Guid Id,
    [property: Required, MaxLength(50)] string Name,
    IReadOnlyList<string>? Options,
    int Position,
    bool Active) : ICommand<EntityFieldDefDto>;

/// <summary>
/// A hard delete. Values already stored under this definition are ignored on read and dropped the next
/// time the entity is saved, so there is nothing to clean up and no tombstone to reason about.
/// The field type is deliberately not editable: changing Text to Number would leave every stored value
/// invalid, and deleting plus recreating says what is actually happening.
/// </summary>
public sealed record DeleteEntityFieldDefCommand(Guid Id) : ICommand<bool>;
