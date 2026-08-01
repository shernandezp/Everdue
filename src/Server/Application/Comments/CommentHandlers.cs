using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Comments;

public sealed class ListCommentsHandler(IEverdueDbContext db, IUserDirectory users)
    : IRequestHandler<ListCommentsQuery, IReadOnlyList<CommentDto>>
{
    public async Task<IReadOnlyList<CommentDto>> Handle(ListCommentsQuery request, CancellationToken cancellationToken = default)
    {
        if (!await db.WorkItems.AnyAsync(w => w.Id == request.WorkItemId, cancellationToken))
        {
            throw new NotFoundException(ResourceNames.WorkItem, request.WorkItemId);
        }

        var rows = await db.Comments.AsNoTracking()
            .Where(c => c.WorkItemId == request.WorkItemId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Id, c.WorkItemId, c.UserId, c.Body, c.CreatedAt })
            .ToListAsync(cancellationToken);

        var directory = await users.MapAsync(rows.Select(r => r.UserId), cancellationToken);

        return rows
            .Select(r => new CommentDto(
                r.Id,
                r.WorkItemId,
                r.UserId,
                directory.TryGetValue(r.UserId, out var user) ? user.DisplayName : "—",
                r.Body,
                r.CreatedAt))
            .ToArray();
    }
}

public sealed class AddCommentHandler(
    IEverdueDbContext db,
    ICurrentUser currentUser,
    IUserDirectory users,
    INotificationEnqueuer notifications,
    IClock clock)
    : IRequestHandler<AddCommentCommand, CommentDto>
{
    public async Task<CommentDto> Handle(AddCommentCommand request, CancellationToken cancellationToken = default)
    {
        var item = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == request.WorkItemId, cancellationToken)
                   ?? throw new NotFoundException(ResourceNames.WorkItem, request.WorkItemId);

        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var comment = new Comment
        {
            Id = Guid.CreateVersion7(),
            WorkItemId = item.Id,
            UserId = userId,
            Body = request.Body.Trim(),
            CreatedAt = now,
        };

        db.Comments.Add(comment);
        db.WorkItemEvents.Add(WorkItemEventFactory.CommentAdded(item, userId, now, comment.Id));

        await NotifyMentionsAsync(request, item, comment, userId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var author = await users.FindAsync(userId, cancellationToken);
        return new CommentDto(comment.Id, comment.WorkItemId, userId, author?.DisplayName ?? "—", comment.Body, comment.CreatedAt);
    }

    /// <summary>
    /// Anyone active in the tenant may be mentioned — a fifteen-person company mentions across
    /// teams, and scoping it to a department would mostly produce "why can't I tag him".
    /// </summary>
    private async Task NotifyMentionsAsync(
        AddCommentCommand request,
        WorkItem item,
        Comment comment,
        Guid authorId,
        CancellationToken cancellationToken)
    {
        if (request.MentionedUserIds is not { Count: > 0 } mentioned)
        {
            return;
        }

        var candidates = mentioned.Distinct().Where(id => id != authorId).ToArray();
        if (candidates.Length == 0)
        {
            return;
        }

        var directory = await users.MapAsync(candidates, cancellationToken);

        var requests = candidates
            .Where(id => directory.TryGetValue(id, out var user) && user.Active)
            .Select(id => new NotificationRequest(
                id,
                NotificationType.Mentioned,
                item.Id,
                comment.Id,
                NotificationData.For(
                    (NotificationData.Title, item.Title),
                    (NotificationData.Actor, currentUser.DisplayName))))
            .ToArray();

        await notifications.EnqueueManyAsync(requests, cancellationToken);
    }
}

public sealed class DeleteCommentHandler(IEverdueDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteCommentCommand, bool>
{
    public async Task<bool> Handle(DeleteCommentCommand request, CancellationToken cancellationToken = default)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
                      ?? throw new NotFoundException(ResourceNames.Comment, request.Id);

        if (!currentUser.IsAdmin && comment.UserId != currentUser.RequireUserId())
        {
            throw new ForbiddenException("Only the author or an administrator can delete this comment.");
        }

        db.Comments.Remove(comment);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
