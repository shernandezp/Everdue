using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Imports;

/// <summary>
/// Importing the open tasks a team is carrying in a spreadsheet.
///
/// One-off tasks only, always created <c>Open</c>: occurrences are engine-created and can never be imported,
/// which is what keeps the ledger something the engine alone writes. Entities and departments must already
/// exist — an import that silently created references would be a second, unreviewed way of adding them.
///
/// Each created row gets the ordinary <c>Created</c> event with the importing administrator as actor, so
/// imported work is visible in the ledger exactly like work anybody typed in.
/// </summary>
public sealed class WorkItemImportHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    ICurrentUser currentUser,
    ITenantProvider tenants,
    IClock clock,
    IOptions<ImportOptions> options)
{
    public static IReadOnlyList<ImportFieldDto> Fields { get; } =
    [
        new(ImportFields.Title, "Title", true, null),
        new(ImportFields.DueDate, "Due date", true, "yyyy-MM-dd, or a date your locale writes"),
        new(ImportFields.Owner, "Owner", false, "e-mail or display name; defaults to you"),
        new(ImportFields.Entity, "Entity", false, "must already exist"),
        new(ImportFields.Department, "Department", false, "must already exist"),
        new(ImportFields.Description, "Description", false, null),
    ];

    public static IReadOnlyDictionary<string, string[]> Aliases { get; } = new Dictionary<string, string[]>
    {
        [ImportFields.Title] = ["titulo", "tarea", "task", "asunto", "subject"],
        [ImportFields.DueDate] = ["fecha", "vencimiento", "due", "fecha limite", "deadline"],
        [ImportFields.Owner] = ["responsable", "asignado", "assignee", "propietario"],
        [ImportFields.Entity] = ["cliente", "customer", "proveedor", "supplier", "entidad", "equipo"],
        [ImportFields.Department] = ["departamento", "area", "área", "team", "equipo de trabajo"],
        [ImportFields.Description] = ["descripcion", "descripción", "notas", "notes", "detalle"],
    };

    /// <summary>Names resolved once for the whole file, so a 5 000-row import is not 15 000 lookups.</summary>
    public sealed record Lookups(
        IReadOnlyDictionary<string, Guid> UsersByEmail,
        IReadOnlyDictionary<string, Guid> UsersByName,
        IReadOnlyDictionary<string, Guid> Entities,
        IReadOnlyDictionary<string, Guid> Departments,
        TimeZoneInfo TimeZone);

    public async Task<Lookups> LoadLookupsAsync(CancellationToken cancellationToken)
    {
        var directory = await users.ListAsync(includeInactive: false, cancellationToken);

        var entities = await db.Entities.AsNoTracking()
            .Where(e => e.Active)
            .Select(e => new { e.Id, e.Name })
            .ToListAsync(cancellationToken);

        var departments = await db.Departments.AsNoTracking()
            .Where(d => d.Active)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(cancellationToken);

        return new Lookups(
            Index(directory.Select(u => (u.Email, u.Id))),
            Index(directory.Select(u => (u.DisplayName, u.Id))),
            Index(entities.Select(e => (e.Name, e.Id))),
            Index(departments.Select(d => (d.Name, d.Id))),
            await tenants.GetTimeZoneAsync(cancellationToken));
    }

    /// <summary>
    /// Last one wins on a duplicate name rather than throwing: two people called "J. Pérez" is a directory
    /// problem, not an import failure, and the mapping form lets somebody use e-mail instead.
    /// </summary>
    private static Dictionary<string, Guid> Index(IEnumerable<(string Key, Guid Id)> pairs)
    {
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, id) in pairs)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                map[key.Trim()] = id;
            }
        }

        return map;
    }

    public (WorkItemDraft? Draft, string? Error) Parse(string[] row, ImportMapping mapping, Lookups lookups)
    {
        var title = mapping.Value(row, ImportFields.Title);

        if (string.IsNullOrWhiteSpace(title))
        {
            return (null, "Title is required.");
        }

        if (title.Length > 300)
        {
            return (null, "Title is longer than 300 characters.");
        }

        var dueText = mapping.Value(row, ImportFields.DueDate);

        if (!LocalizedValues.TryParseDate(dueText, out var due))
        {
            return (null, $"'{dueText}' is not a date. Use yyyy-MM-dd.");
        }

        var owner = currentUser.RequireUserId();

        if (mapping.Value(row, ImportFields.Owner) is { } ownerText)
        {
            if (lookups.UsersByEmail.TryGetValue(ownerText, out var byEmail))
            {
                owner = byEmail;
            }
            else if (lookups.UsersByName.TryGetValue(ownerText, out var byName))
            {
                owner = byName;
            }
            else
            {
                return (null, $"No active user matches '{ownerText}'.");
            }
        }

        Guid? entityId = null;

        if (mapping.Value(row, ImportFields.Entity) is { } entityText)
        {
            if (!lookups.Entities.TryGetValue(entityText, out var found))
            {
                return (null, $"No active entity named '{entityText}'. Create it, or import entities first.");
            }

            entityId = found;
        }

        Guid? departmentId = null;

        if (mapping.Value(row, ImportFields.Department) is { } departmentText)
        {
            if (!lookups.Departments.TryGetValue(departmentText, out var found))
            {
                return (null, $"No active department named '{departmentText}'.");
            }

            departmentId = found;
        }

        var description = mapping.Value(row, ImportFields.Description);

        if (description is { Length: > 4000 })
        {
            return (null, "Description is longer than 4000 characters.");
        }

        // The end of the chosen day in the tenant's zone, matching what an occurrence's due date means.
        var dueInstant = TenantTime.EndOfDay(due, lookups.TimeZone);

        return (new WorkItemDraft(title.Trim(), description, owner, entityId, departmentId, dueInstant), null);
    }

    public async Task<ImportResultDto> CommitAsync(
        CsvTable table,
        ImportMapping mapping,
        CancellationToken cancellationToken)
    {
        var lookups = await LoadLookupsAsync(cancellationToken);
        var now = clock.UtcNow;
        var actor = currentUser.RequireUserId();

        var created = 0;
        var failed = 0;
        var failures = new List<ImportRowFailureDto>();
        var maxFailures = options.Value.MaxReportedFailures;

        for (var index = 0; index < table.Rows.Count; index++)
        {
            var rowNumber = index + 2;

            var (draft, error) = Parse(table.Rows[index], mapping, lookups);

            if (draft is null)
            {
                failed++;
                if (failures.Count < maxFailures)
                {
                    failures.Add(new ImportRowFailureDto(rowNumber, error ?? "Could not be read."));
                }

                continue;
            }

            var item = new WorkItem
            {
                Id = Guid.CreateVersion7(),
                ResponsibilityId = null,
                Title = draft.Title,
                Description = draft.Description,
                OwnerUserId = draft.OwnerUserId,
                EntityId = draft.EntityId,
                DepartmentId = draft.DepartmentId,
                DueDate = draft.DueDate,
                Status = WorkItemStatus.Open,
                CreatedAt = now,
            };

            db.WorkItems.Add(item);

            db.WorkItemEvents.Add(WorkItemEventFactory.Created(
                item,
                actor,
                now,
                new { source = WorkItemSources.OneOff, import = true }));

            created++;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Nothing is ever skipped as a duplicate here: two tasks with the same title are a normal thing for a
        // team to have, and de-duplicating them would silently drop real work.
        return new ImportResultDto(created, 0, failed, failures);
    }

}

public sealed record WorkItemDraft(
    string Title,
    string? Description,
    Guid OwnerUserId,
    Guid? EntityId,
    Guid? DepartmentId,
    DateTimeOffset DueDate);
