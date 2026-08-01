using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Entities;

/// <summary>
/// Reads and writes the one JSON column that holds an entity's custom values.
///
/// The whole point of routing every path — the entity form, the CSV import, the demo seeder — through
/// here is that validation happens once. A value that could not be typed into the form cannot be
/// imported into the database either.
/// </summary>
public sealed class EntityCustomFieldWriter(IEverdueDbContext db)
{
    public Task<List<EntityFieldDef>> DefinitionsForAsync(EntityType entityType, CancellationToken cancellationToken)
        => db.EntityFieldDefs.AsNoTracking()
            .Where(d => d.EntityType == entityType && d.Active)
            .OrderBy(d => d.Position)
            .ToListAsync(cancellationToken);

    public Task<List<EntityFieldDef>> AllDefinitionsAsync(CancellationToken cancellationToken)
        => db.EntityFieldDefs.AsNoTracking()
            .Where(d => d.Active)
            .OrderBy(d => d.EntityType)
            .ThenBy(d => d.Position)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Validates the submitted values against the type's definitions and returns the column content.
    /// Unknown keys are dropped rather than rejected: a definition somebody deleted five minutes ago must
    /// not make an otherwise valid entity save fail.
    /// </summary>
    public string? Merge(
        IReadOnlyList<EntityFieldDef> definitions,
        IReadOnlyDictionary<Guid, string?>? submitted,
        string? existingJson)
    {
        if (submitted is null)
        {
            // No custom-field section in the request at all: leave whatever is stored untouched, so a
            // client that does not know about custom fields cannot silently erase them.
            return existingJson;
        }

        var errors = new Dictionary<string, string[]>();
        var values = new Dictionary<Guid, string>();

        foreach (var definition in definitions)
        {
            if (!submitted.TryGetValue(definition.Id, out var raw))
            {
                continue;
            }

            var result = EntityCustomFields.Validate(definition, raw);

            if (!result.Ok)
            {
                errors[definition.Name] = [result.Error!];
                continue;
            }

            if (result.Normalized is { } normalized)
            {
                values[definition.Id] = normalized;
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return EntityCustomFields.Serialize(values);
    }

    /// <summary>Resolves a stored column into what the client renders: label, type, options, value.</summary>
    public static IReadOnlyList<EntityCustomFieldValueDto> Resolve(
        IReadOnlyList<EntityFieldDef> definitions,
        EntityType entityType,
        string? json)
    {
        var stored = EntityCustomFields.Parse(json);

        return definitions
            .Where(d => d.EntityType == entityType)
            .OrderBy(d => d.Position)
            .Select(d => new EntityCustomFieldValueDto(
                d.Id,
                d.Name,
                d.FieldType,
                EntityCustomFields.ParseOptions(d.OptionsJson),
                d.Position,
                stored.TryGetValue(d.Id, out var value) ? value : null))
            .ToArray();
    }
}
