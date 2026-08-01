using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.WorkItems;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Responsibilities;

/// <summary>
/// The genuinely new part of v1.5's reassignment: handing over a **responsibility**, not one
/// occurrence. Future occurrences follow automatically because the engine copies the owner at spawn,
/// so nothing has to be back-filled — only the work already on somebody's plate needs a decision,
/// which is what the flag is for.
/// </summary>
public sealed record ReassignResponsibilityCommand(
    Guid Id,
    Guid NewOwnerUserId,
    bool ApplyToWorkableOccurrences) : ICommand<ReassignResultDto>;

public sealed class ReassignResponsibilityHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    ICurrentUser currentUser,
    INotificationEnqueuer notifications,
    IWebhookPublisher webhooks,
    IClock clock) : IRequestHandler<ReassignResponsibilityCommand, ReassignResultDto>
{
    public async Task<ReassignResultDto> Handle(ReassignResponsibilityCommand request, CancellationToken cancellationToken = default)
    {
        var responsibility = await db.Responsibilities.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                             ?? throw new NotFoundException(ResourceNames.Responsibility, request.Id);

        await users.RequireAssignableAsync(request.NewOwnerUserId, cancellationToken);

        var actor = currentUser.RequireUserId();
        var actorName = currentUser.DisplayName ?? "—";
        var now = clock.UtcNow;

        var previousOwner = responsibility.OwnerUserId;
        responsibility.OwnerUserId = request.NewOwnerUserId;

        var moved = 0;

        if (request.ApplyToWorkableOccurrences)
        {
            // Workable, not merely outstanding: a missed occurrence still needs somebody to complete
            // it late, and leaving it with the person who has gone is how it never happens.
            var occurrences = await db.WorkItems
                .Where(w => w.ResponsibilityId == responsibility.Id
                            && WorkItemQueries.Workable.Contains(w.Status)
                            && w.OwnerUserId != request.NewOwnerUserId)
                .ToListAsync(cancellationToken);

            foreach (var occurrence in occurrences)
            {
                await Reassignment.ApplyAsync(db, webhooks, occurrence, request.NewOwnerUserId, actor, now, cancellationToken);
                moved++;
            }

            await notifications.EnqueueManyAsync(
                occurrences.Select(o => Reassignment.Notify(o, request.NewOwnerUserId, actor, actorName)).ToArray(),
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return previousOwner == request.NewOwnerUserId
            ? new ReassignResultDto(0, moved)
            : new ReassignResultDto(1, moved);
    }
}
