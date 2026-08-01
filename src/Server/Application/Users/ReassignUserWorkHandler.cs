using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Users;

/// <summary>
/// The departure path: everything one person owns becomes somebody else's, in one call.
///
/// A first-class action rather than a loop in the browser because it is the moment the tool is most
/// likely to be used under time pressure — somebody left, and their responsibilities are still
/// spawning occurrences every morning.
/// </summary>
public sealed record ReassignUserWorkCommand(
    Guid Id,
    Guid ToUserId,
    bool IncludeResponsibilities = true,
    bool IncludeWorkableItems = true) : ICommand<ReassignResultDto>;

public sealed class ReassignUserWorkHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    ICurrentUser currentUser,
    INotificationEnqueuer notifications,
    IWebhookPublisher webhooks,
    IClock clock) : IRequestHandler<ReassignUserWorkCommand, ReassignResultDto>
{
    public async Task<ReassignResultDto> Handle(ReassignUserWorkCommand request, CancellationToken cancellationToken = default)
    {
        if (request.Id == request.ToUserId)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["toUserId"] = ["Choose somebody other than the current owner."],
            });
        }

        // The person leaving may already be deactivated, so they are only checked for existence;
        // the person arriving has to be assignable, which is the whole point of the check.
        _ = await users.FindAsync(request.Id, cancellationToken) ?? throw new NotFoundException(ResourceNames.User, request.Id);
        await users.RequireAssignableAsync(request.ToUserId, cancellationToken);

        var actor = currentUser.RequireUserId();
        var actorName = currentUser.DisplayName ?? "—";
        var now = clock.UtcNow;

        var responsibilities = 0;

        if (request.IncludeResponsibilities)
        {
            var owned = await db.Responsibilities
                .Where(r => r.OwnerUserId == request.Id)
                .ToListAsync(cancellationToken);

            foreach (var responsibility in owned)
            {
                responsibility.OwnerUserId = request.ToUserId;
                responsibilities++;
            }
        }

        var moved = 0;

        if (request.IncludeWorkableItems)
        {
            var items = await db.WorkItems
                .Where(w => w.OwnerUserId == request.Id && WorkItemQueries.Workable.Contains(w.Status))
                .ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                await Reassignment.ApplyAsync(db, webhooks, item, request.ToUserId, actor, now, cancellationToken);
                moved++;
            }

            // One notification per item would be a hundred messages on somebody's first morning
            // covering for a colleague. One is enough; the list itself is on their board.
            if (items.Count == 1)
            {
                await notifications.EnqueueAsync(
                    Reassignment.Notify(items[0], request.ToUserId, actor, actorName),
                    cancellationToken);
            }
            else if (items.Count > 1)
            {
                await notifications.EnqueueAsync(
                    new NotificationRequest(
                        request.ToUserId,
                        Domain.NotificationType.Assigned,
                        Data: NotificationData.For(
                            (NotificationData.Title, $"{items.Count}"),
                            (NotificationData.Actor, actorName))),
                    cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ReassignResultDto(responsibilities, moved);
    }
}
