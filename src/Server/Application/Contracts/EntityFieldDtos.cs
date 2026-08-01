using Everdue.Server.Domain;

namespace Everdue.Server.Application.Contracts;

/// <summary>A custom field definition, as the admin screen and the entity form read it.</summary>
public sealed record EntityFieldDefDto(
    Guid Id,
    EntityType EntityType,
    string Name,
    EntityFieldType FieldType,
    IReadOnlyList<string> Options,
    int Position,
    bool Active);

/// <summary>
/// One resolved value on an entity: the definition it belongs to, so the client renders a label and an
/// input type without a second lookup.
/// </summary>
public sealed record EntityCustomFieldValueDto(
    Guid DefinitionId,
    string Name,
    EntityFieldType FieldType,
    IReadOnlyList<string> Options,
    int Position,
    string? Value);
