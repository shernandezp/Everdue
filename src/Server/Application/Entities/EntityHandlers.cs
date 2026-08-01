using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Entities;

/// <summary>
/// One narrow projection, so list and single reads see the same columns. The custom-field JSON travels
/// with the row — it is a column already on it, so resolving it costs no extra query.
/// </summary>
internal sealed record EntityRow(Guid Id, string Name, EntityType Type, bool Active, string? CustomFieldsJson);

internal static class EntityMapping
{
    public static IQueryable<EntityRow> Project(IQueryable<Entity> query)
        => query.Select(e => new EntityRow(e.Id, e.Name, e.Type, e.Active, e.CustomFieldsJson));

    public static EntityDto ToDto(EntityRow row, IReadOnlyList<EntityFieldDef> definitions)
        => new(
            row.Id,
            row.Name,
            row.Type,
            row.Active,
            EntityCustomFieldWriter.Resolve(definitions, row.Type, row.CustomFieldsJson));

    public static EntityDto ToDto(Entity entity, IReadOnlyList<EntityFieldDef> definitions)
        => ToDto(new EntityRow(entity.Id, entity.Name, entity.Type, entity.Active, entity.CustomFieldsJson), definitions);
}

public sealed class ListEntitiesHandler(IEverdueDbContext db, EntityCustomFieldWriter customFields)
    : IRequestHandler<ListEntitiesQuery, PagedResult<EntityDto>>
{
    public async Task<PagedResult<EntityDto>> Handle(ListEntitiesQuery request, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);

        var query = db.Entities.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(e => e.Active);
        }

        if (request.ResolvedType is { } type)
        {
            query = query.Where(e => e.Type == type);
        }

        if (SearchPattern.For(request.Search) is { } pattern)
        {
            query = query.Where(e => EF.Functions.Like(e.Name.ToLower(), pattern, SearchPattern.Escape));
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await EntityMapping
            .Project(query.OrderBy(e => e.Type).ThenBy(e => e.Name).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);

        // One read of a tiny table for the whole page, not one per row.
        var definitions = await customFields.AllDefinitionsAsync(cancellationToken);

        var items = rows.Select(row => EntityMapping.ToDto(row, definitions)).ToArray();
        return new PagedResult<EntityDto>(items, total, page, pageSize);
    }
}

public sealed class GetEntityHandler(IEverdueDbContext db, EntityCustomFieldWriter customFields)
    : IRequestHandler<GetEntityQuery, EntityDto>
{
    public async Task<EntityDto> Handle(GetEntityQuery request, CancellationToken cancellationToken = default)
    {
        var row = await EntityMapping.Project(db.Entities.AsNoTracking().Where(e => e.Id == request.Id))
                      .FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException(ResourceNames.Entity, request.Id);

        var definitions = await customFields.DefinitionsForAsync(row.Type, cancellationToken);
        return EntityMapping.ToDto(row, definitions);
    }
}

public sealed class CreateEntityHandler(
    IEverdueDbContext db,
    EntityCustomFieldWriter customFields,
    IWebhookPublisher webhooks) : IRequestHandler<CreateEntityCommand, EntityDto>
{
    public async Task<EntityDto> Handle(CreateEntityCommand request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (await db.Entities.AnyAsync(e => e.Type == request.Type && e.Name == name, cancellationToken))
        {
            throw new ValidationException($"An entity of type {request.Type} named '{name}' already exists.");
        }

        var definitions = await customFields.DefinitionsForAsync(request.Type, cancellationToken);

        var entity = new Entity
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Type = request.Type,
            Active = true,
            CustomFieldsJson = customFields.Merge(definitions, request.CustomFields, null),
        };

        db.Entities.Add(entity);

        // The one entity-side webhook call site. In the same commit as the row, like every other one.
        await webhooks.PublishEntityAsync(WebhookEventType.EntityCreated, entity, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return EntityMapping.ToDto(entity, definitions);
    }
}

public sealed class UpdateEntityHandler(IEverdueDbContext db, EntityCustomFieldWriter customFields)
    : IRequestHandler<UpdateEntityCommand, EntityDto>
{
    public async Task<EntityDto> Handle(UpdateEntityCommand request, CancellationToken cancellationToken = default)
    {
        var entity = await db.Entities.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
                     ?? throw new NotFoundException(ResourceNames.Entity, request.Id);

        var name = request.Name.Trim();

        if (await db.Entities.AnyAsync(e => e.Id != request.Id && e.Type == request.Type && e.Name == name, cancellationToken))
        {
            throw new ValidationException($"An entity of type {request.Type} named '{name}' already exists.");
        }

        // Definitions belong to the *new* type: changing a customer into a supplier changes which fields
        // apply, and the merge drops the ones that no longer do.
        var definitions = await customFields.DefinitionsForAsync(request.Type, cancellationToken);

        entity.Name = name;
        entity.Type = request.Type;
        entity.Active = request.Active;
        entity.CustomFieldsJson = customFields.Merge(definitions, request.CustomFields, entity.CustomFieldsJson);

        await db.SaveChangesAsync(cancellationToken);

        return EntityMapping.ToDto(entity, definitions);
    }
}

public sealed class DeactivateEntityHandler(IEverdueDbContext db, EntityCustomFieldWriter customFields)
    : IRequestHandler<DeactivateEntityCommand, EntityDto>
{
    public async Task<EntityDto> Handle(DeactivateEntityCommand request, CancellationToken cancellationToken = default)
    {
        var entity = await db.Entities.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
                     ?? throw new NotFoundException(ResourceNames.Entity, request.Id);

        entity.Active = false;
        await db.SaveChangesAsync(cancellationToken);

        var definitions = await customFields.DefinitionsForAsync(entity.Type, cancellationToken);
        return EntityMapping.ToDto(entity, definitions);
    }
}
