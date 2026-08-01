using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Checklists;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.WorkItems;

public sealed class ListWorkItemsHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    IClock clock,
    ChecklistProgressReader checklists)
    : IRequestHandler<ListWorkItemsQuery, PagedResult<WorkItemDto>>
{
    public async Task<PagedResult<WorkItemDto>> Handle(ListWorkItemsQuery request, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);

        var query = WorkItemQueries.Filter(db.WorkItems.AsNoTracking(), request, now);
        var total = await query.CountAsync(cancellationToken);

        var ordered = query.OrderBy(w => w.DueDate).ThenBy(w => w.Id);

        var rows = await WorkItemQueries
            .Project(ordered.Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);

        var items = await WorkItemQueries.ToDtosAsync(rows, users, now, cancellationToken, checklists);
        return new PagedResult<WorkItemDto>(items, total, page, pageSize);
    }
}

public sealed class GetWorkItemHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    IClock clock,
    ChecklistProgressReader checklists,
    ChecklistItemAccess checklistAccess,
    CompletionPreconditions preconditions)
    : IRequestHandler<GetWorkItemQuery, WorkItemDetailDto>
{
    public async Task<WorkItemDetailDto> Handle(GetWorkItemQuery request, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var row = await WorkItemQueries
                      .Project(db.WorkItems.AsNoTracking().Where(w => w.Id == request.Id))
                      .FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException(ResourceNames.WorkItem, request.Id);

        var events = await db.WorkItemEvents.AsNoTracking()
            .Where(e => e.WorkItemId == request.Id)
            .OrderBy(e => e.Timestamp)
            .ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.UserId, e.Timestamp, e.EventType, e.FromStatus, e.ToStatus, e.DataJson })
            .ToListAsync(cancellationToken);

        var comments = await db.Comments.AsNoTracking()
            .Where(c => c.WorkItemId == request.Id)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Id, c.WorkItemId, c.UserId, c.Body, c.CreatedAt })
            .ToListAsync(cancellationToken);

        var userIds = events.Where(e => e.UserId is not null).Select(e => e.UserId!.Value)
            .Concat(comments.Select(c => c.UserId))
            .Append(row.OwnerUserId)
            .Concat(row.CompletedByUserId is { } id ? [id] : Array.Empty<Guid>());

        var directory = await users.MapAsync(userIds, cancellationToken);

        var checklist = await checklistAccess.ListAsync(request.Id, cancellationToken);
        var progress = await checklists.ForAsync([request.Id], cancellationToken);

        // Reported rather than hidden: `Completed` stays in the allowed transitions because it is one,
        // and the requirements object is what lets the drawer disable the button with a reason. Described from the
        // projected row's ids, so opening a drawer costs no extra read of the entity.
        var requirements = await preconditions.DescribeAsync(row.Id, row.ResponsibilityId, cancellationToken);

        return new WorkItemDetailDto(
            WorkItemQueries.ToDto(row, directory, now, progress),
            events.Select(e => new WorkItemEventDto(
                e.Id,
                e.UserId,
                e.UserId is { } uid && directory.TryGetValue(uid, out var eventUser) ? eventUser.DisplayName : null,
                e.Timestamp,
                e.EventType,
                e.FromStatus,
                e.ToStatus,
                e.DataJson)).ToArray(),
            comments.Select(c => new CommentDto(
                c.Id,
                c.WorkItemId,
                c.UserId,
                directory.TryGetValue(c.UserId, out var commentUser) ? commentUser.DisplayName : "—",
                c.Body,
                c.CreatedAt)).ToArray(),
            StatusTransitions.UserTransitionsFrom(row.Status, row.ResponsibilityId is not null),
            checklist,
            requirements);
    }
}
