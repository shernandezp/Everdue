using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Checklists;

/// <summary>
/// Loading, authorising and projecting checklist lines. Shared by the four handlers below so each of
/// them is the rule it exists for and nothing else.
/// </summary>
public sealed class ChecklistItemAccess(IEverdueDbContext db, IUserDirectory users)
{
    /// <summary>
    /// A checklist may be worked while the item is workable — Open, InProgress, OnHold or Missed, since a
    /// missed occurrence still has to be completed late. Once it is completed or cancelled the list is
    /// part of the record, and reopening makes it editable again <em>without</em> clearing what was
    /// ticked: the work really was done.
    /// </summary>
    public async Task<WorkItem> LoadEditableItemAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var item = await db.WorkItems.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workItemId, cancellationToken)
                   ?? throw new NotFoundException(ResourceNames.WorkItem, workItemId);

        if (!item.Status.IsWorkable())
        {
            throw new ConflictException(
                $"This item is {item.Status}; its checklist is part of the record and can no longer be changed.");
        }

        return item;
    }

    public async Task<ChecklistItem> LoadLineAsync(Guid workItemId, Guid itemId, CancellationToken cancellationToken)
        => await db.ChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId && c.WorkItemId == workItemId, cancellationToken)
           ?? throw new NotFoundException(ResourceNames.ChecklistItem, itemId);

    public async Task<IReadOnlyList<ChecklistItemDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var lines = await db.ChecklistItems.AsNoTracking()
            .Where(c => c.WorkItemId == workItemId)
            .OrderBy(c => c.Position)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(lines, cancellationToken);
    }

    public async Task<IReadOnlyList<ChecklistItemDto>> ToDtosAsync(
        IReadOnlyList<ChecklistItem> lines,
        CancellationToken cancellationToken)
    {
        var directory = await users.MapAsync(
            lines.Where(c => c.CheckedByUserId is not null).Select(c => c.CheckedByUserId!.Value),
            cancellationToken);

        return lines.Select(line => ToDto(line, directory)).ToArray();
    }

    public static ChecklistItemDto ToDto(ChecklistItem line, IReadOnlyDictionary<Guid, UserSummary> directory)
        => new(
            line.Id,
            line.WorkItemId,
            line.Text,
            line.Required,
            line.Position,
            line.CheckedAt,
            line.CheckedByUserId,
            line.CheckedByUserId is { } id && directory.TryGetValue(id, out var user) ? user.DisplayName : null);
}

public sealed class ListChecklistItemsHandler(IEverdueDbContext db, ChecklistItemAccess access)
    : IRequestHandler<ListChecklistItemsQuery, IReadOnlyList<ChecklistItemDto>>
{
    public async Task<IReadOnlyList<ChecklistItemDto>> Handle(
        ListChecklistItemsQuery request,
        CancellationToken cancellationToken = default)
    {
        if (!await db.WorkItems.AnyAsync(w => w.Id == request.WorkItemId, cancellationToken))
        {
            throw new NotFoundException(ResourceNames.WorkItem, request.WorkItemId);
        }

        return await access.ListAsync(request.WorkItemId, cancellationToken);
    }
}

public sealed class AddChecklistItemHandler(
    IEverdueDbContext db,
    ChecklistItemAccess access,
    IUserDirectory users,
    IOptions<ChecklistOptions> options) : IRequestHandler<AddChecklistItemCommand, ChecklistItemDto>
{
    public async Task<ChecklistItemDto> Handle(AddChecklistItemCommand request, CancellationToken cancellationToken = default)
    {
        var item = await access.LoadEditableItemAsync(request.WorkItemId, cancellationToken);

        var text = request.Text.Trim();
        if (text.Length == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["text"] = ["A checklist item cannot be blank."] });
        }

        var lines = await db.ChecklistItems.AsNoTracking()
            .Where(c => c.WorkItemId == item.Id)
            .Select(c => c.Position)
            .ToListAsync(cancellationToken);

        var max = options.Value.MaxItemsPerWorkItem;
        if (lines.Count >= max)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["text"] = [$"This item already has the maximum of {max} checklist items."],
            });
        }

        var line = new ChecklistItem
        {
            Id = Guid.CreateVersion7(),
            WorkItemId = item.Id,
            Text = text,

            // Never required, whatever was asked. Only the template decides what blocks a completion,
            // so nobody can bolt a gate onto somebody else's item halfway through the period.
            Required = false,
            Position = lines.Count == 0 ? 0 : lines.Max() + 1,
        };

        db.ChecklistItems.Add(line);
        await db.SaveChangesAsync(cancellationToken);

        var directory = await users.MapAsync([], cancellationToken);
        return ChecklistItemAccess.ToDto(line, directory);
    }
}

/// <summary>
/// Ticking a line. No <c>WorkItemEvent</c> is written: the row's own CheckedAt/CheckedBy is the record,
/// and fifteen events per inspection would bury the status history the timeline exists for.
/// </summary>
public sealed class SetChecklistItemCheckedHandler(
    IEverdueDbContext db,
    ChecklistItemAccess access,
    ICurrentUser currentUser,
    IUserDirectory users,
    IClock clock) : IRequestHandler<SetChecklistItemCheckedCommand, ChecklistItemDto>
{
    public async Task<ChecklistItemDto> Handle(
        SetChecklistItemCheckedCommand request,
        CancellationToken cancellationToken = default)
    {
        await access.LoadEditableItemAsync(request.WorkItemId, cancellationToken);

        var line = await access.LoadLineAsync(request.WorkItemId, request.ItemId, cancellationToken);

        if (request.Checked)
        {
            // Re-checking an already checked line keeps the original tick: who did it first is the
            // interesting fact, and overwriting it on a double click would lose it.
            if (!line.IsChecked)
            {
                line.CheckedAt = clock.UtcNow;
                line.CheckedByUserId = currentUser.RequireUserId();
            }
        }
        else
        {
            line.CheckedAt = null;
            line.CheckedByUserId = null;
        }

        await db.SaveChangesAsync(cancellationToken);

        var directory = await users.MapAsync(
            line.CheckedByUserId is { } id ? [id] : [],
            cancellationToken);

        return ChecklistItemAccess.ToDto(line, directory);
    }
}

public sealed class DeleteChecklistItemHandler(IEverdueDbContext db, ChecklistItemAccess access)
    : IRequestHandler<DeleteChecklistItemCommand, bool>
{
    public async Task<bool> Handle(DeleteChecklistItemCommand request, CancellationToken cancellationToken = default)
    {
        await access.LoadEditableItemAsync(request.WorkItemId, cancellationToken);

        var line = await access.LoadLineAsync(request.WorkItemId, request.ItemId, cancellationToken);

        // A required line came from the template, and the template is what the occurrence was asked to
        // do. Removing it would edit the obligation after the fact.
        if (line.Required)
        {
            throw new ConflictException(
                "A required checklist item comes from the responsibility's template and cannot be removed from a single occurrence.");
        }

        db.ChecklistItems.Remove(line);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
