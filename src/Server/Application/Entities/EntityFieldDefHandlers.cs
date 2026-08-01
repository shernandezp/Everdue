using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Entities;

internal static class EntityFieldDefMapping
{
    public static EntityFieldDefDto ToDto(EntityFieldDef def)
        => new(
            def.Id,
            def.EntityType,
            def.Name,
            def.FieldType,
            EntityCustomFields.ParseOptions(def.OptionsJson),
            def.Position,
            def.Active);

    /// <summary>
    /// Select fields need options and the others must not have them: an option list on a date field is a
    /// contradiction the form would then have to render.
    /// </summary>
    public static string? ValidateOptions(EntityFieldType fieldType, IReadOnlyList<string>? options)
    {
        if (fieldType != EntityFieldType.Select)
        {
            return null;
        }

        var cleaned = (options ?? [])
            .Select(option => option.Trim())
            .Where(option => option.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (cleaned.Length == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["options"] = ["A Select field needs at least one option."],
            });
        }

        if (cleaned.Length > EntityCustomFields.MaxSelectOptions)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["options"] = [$"A Select field may offer at most {EntityCustomFields.MaxSelectOptions} options."],
            });
        }

        return EntityCustomFields.SerializeOptions(cleaned);
    }
}

public sealed class ListEntityFieldDefsHandler(IEverdueDbContext db)
    : IRequestHandler<ListEntityFieldDefsQuery, IReadOnlyList<EntityFieldDefDto>>
{
    public async Task<IReadOnlyList<EntityFieldDefDto>> Handle(
        ListEntityFieldDefsQuery request,
        CancellationToken cancellationToken = default)
    {
        var query = db.EntityFieldDefs.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(d => d.Active);
        }

        if (request.ResolvedEntityType is { } entityType)
        {
            query = query.Where(d => d.EntityType == entityType);
        }

        var defs = await query
            .OrderBy(d => d.EntityType)
            .ThenBy(d => d.Position)
            .ToListAsync(cancellationToken);

        return defs.Select(EntityFieldDefMapping.ToDto).ToArray();
    }
}

public sealed class CreateEntityFieldDefHandler(IEverdueDbContext db, IOptions<EntityFieldOptions> options)
    : IRequestHandler<CreateEntityFieldDefCommand, EntityFieldDefDto>
{
    public async Task<EntityFieldDefDto> Handle(CreateEntityFieldDefCommand request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var max = options.Value.MaxPerEntityType;

        var existing = await db.EntityFieldDefs
            .Where(d => d.EntityType == request.EntityType)
            .Select(d => new { d.Name, d.Position })
            .ToListAsync(cancellationToken);

        if (existing.Count >= max)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["name"] = [$"{request.EntityType} entities already have the maximum of {max} custom fields."],
            });
        }

        if (existing.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["name"] = [$"A custom field named '{name}' already exists for {request.EntityType} entities."],
            });
        }

        var def = new EntityFieldDef
        {
            Id = Guid.CreateVersion7(),
            EntityType = request.EntityType,
            Name = name,
            FieldType = request.FieldType,
            OptionsJson = EntityFieldDefMapping.ValidateOptions(request.FieldType, request.Options),
            Position = existing.Count == 0 ? 0 : existing.Max(d => d.Position) + 1,
            Active = true,
        };

        db.EntityFieldDefs.Add(def);
        await db.SaveChangesAsync(cancellationToken);

        return EntityFieldDefMapping.ToDto(def);
    }
}

public sealed class UpdateEntityFieldDefHandler(IEverdueDbContext db)
    : IRequestHandler<UpdateEntityFieldDefCommand, EntityFieldDefDto>
{
    public async Task<EntityFieldDefDto> Handle(UpdateEntityFieldDefCommand request, CancellationToken cancellationToken = default)
    {
        var def = await db.EntityFieldDefs.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException(ResourceNames.EntityFieldDef, request.Id);

        var name = request.Name.Trim();

        if (await db.EntityFieldDefs.AnyAsync(
                d => d.Id != def.Id && d.EntityType == def.EntityType && d.Name.ToLower() == name.ToLower(),
                cancellationToken))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["name"] = [$"A custom field named '{name}' already exists for {def.EntityType} entities."],
            });
        }

        def.Name = name;
        def.OptionsJson = EntityFieldDefMapping.ValidateOptions(def.FieldType, request.Options);
        def.Position = request.Position;
        def.Active = request.Active;

        await db.SaveChangesAsync(cancellationToken);
        return EntityFieldDefMapping.ToDto(def);
    }
}

public sealed class DeleteEntityFieldDefHandler(IEverdueDbContext db)
    : IRequestHandler<DeleteEntityFieldDefCommand, bool>
{
    public async Task<bool> Handle(DeleteEntityFieldDefCommand request, CancellationToken cancellationToken = default)
    {
        var def = await db.EntityFieldDefs.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException(ResourceNames.EntityFieldDef, request.Id);

        // Values stored under it are left where they are: unknown keys are ignored on read and dropped on
        // the next write, so a delete needs no migration and no pass over the entity table.
        db.EntityFieldDefs.Remove(def);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
