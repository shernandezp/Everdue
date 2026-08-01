using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Checklists;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Notifications;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.WorkItems;

/// <summary>
/// Shared plumbing for the mutation handlers: load, authorize, transition, notify, save.
/// Transition legality is asked of <see cref="StatusTransitions"/> and answered nowhere else.
///
/// Registered in the container rather than constructed per handler, so a handler's constructor lists
/// what that handler actually needs instead of the five services this class needs.
///
/// Public only because the handlers that take it are: it is plumbing for this folder, and nothing
/// outside <c>Application/WorkItems</c> has any business calling it.
/// </summary>
public sealed class WorkItemMutator(
    IEverdueDbContext db,
    ICurrentUser currentUser,
    IUserDirectory users,
    INotificationEnqueuer notifications,
    IWebhookPublisher webhooks,
    ChecklistProgressReader checklists,
    IClock clock)
{
    private readonly List<(WebhookEventType Type, WorkItem Item, bool Late)> _pendingWebhooks = [];

    public DateTimeOffset Now => clock.UtcNow;

    public Guid ActorId => currentUser.RequireUserId();

    public async Task<WorkItem> LoadAsync(Guid id, CancellationToken cancellationToken)
        => await db.WorkItems.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
           ?? throw new NotFoundException(ResourceNames.WorkItem, id);

    /// <summary>
    /// Undoing someone's completion and cancelling their task are owner-or-admin actions, because
    /// both erase work that was already recorded as done or still to do. Everything else — working
    /// an item, editing it, handing it over — is normal cover and is attributed via an event.
    /// </summary>
    public void RequireOwnerOrAdmin(WorkItem item, string action)
    {
        if (!currentUser.IsAdmin && item.OwnerUserId != currentUser.RequireUserId())
        {
            throw new ForbiddenException($"Only the owner or an administrator can {action} this item.");
        }
    }

    public void Transition(WorkItem item, WorkItemStatus to, object? data = null)
    {
        var from = item.Status;
        var userId = currentUser.RequireUserId();

        if (!StatusTransitions.IsAllowed(from, to, TransitionActor.User, item.IsOccurrence))
        {
            throw new ConflictException(
                $"A {(item.IsOccurrence ? "occurrence" : "task")} cannot go from {from} to {to}.");
        }

        item.Status = to;
        Record(item, WorkItemEventFactory.StatusChanged(item, userId, clock.UtcNow, from, to, data));
    }

    /// <summary>
    /// Writes the event — and, because every work-item mutation already funnels through here, this is
    /// also the single place webhooks are raised from. One call site covering five event types, mapped
    /// from the event that was going to be written anyway: no second event system, no projector, and no
    /// "last processed" cursor that would undo the engine's statelessness.
    ///
    /// The deliveries themselves are created in <see cref="SaveAsync"/>, inside the caller's own commit.
    /// </summary>
    public void Record(WorkItem item, WorkItemEvent @event)
    {
        // Which credential the actor was reached through. Folded into the existing payload rather than added as a
        // column: nothing queries it, and a nullable column on the ledger's busiest table is not free.
        if (currentUser.ApiKeyId is { } apiKeyId)
        {
            @event.DataJson = WorkItemEventFactory.WithApiKey(@event.DataJson, apiKeyId);
        }

        db.WorkItemEvents.Add(@event);

        if (WebhookEvents.From(@event.EventType, @event.ToStatus) is { } webhookType)
        {
            _pendingWebhooks.Add((webhookType, item, item.Status == WorkItemStatus.CompletedLate));
        }
    }

    /// <summary>
    /// Tells somebody something happened to their work — unless they are the one who did it, because
    /// notifying yourself about your own action is the noise that gets a tool muted.
    /// </summary>
    public async Task NotifyOwnerAsync(
        WorkItem item,
        NotificationType type,
        CancellationToken cancellationToken,
        params (string Key, string? Value)[] extraData)
    {
        if (item.OwnerUserId == currentUser.RequireUserId())
        {
            return;
        }

        (string, string?)[] data =
        [
            (NotificationData.Title, item.Title),
            (NotificationData.Actor, currentUser.DisplayName),
            .. extraData.Select(pair => (pair.Key, pair.Value)),
        ];

        await notifications.EnqueueAsync(
            new NotificationRequest(item.OwnerUserId, type, item.Id, Data: NotificationData.For(data)),
            cancellationToken);
    }

    /// <summary>
    /// How every mutation ends: commit, then re-read the item through the same projection the list
    /// and board use. Re-reading rather than mapping the tracked entity is what stops a drawer from
    /// showing a derived field — an overdue flag, an owner's display name — that the list disagrees with.
    /// </summary>
    public async Task<WorkItemDto> SaveAsync(WorkItem item, CancellationToken cancellationToken)
    {
        // Deliveries join the same commit as the change they describe, so a webhook can never exist for
        // work that rolled back.
        await FlushWebhooksAsync(cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await ProjectAsync(item, cancellationToken);
    }

    private async Task FlushWebhooksAsync(CancellationToken cancellationToken)
    {
        foreach (var (type, item, late) in _pendingWebhooks)
        {
            await webhooks.PublishWorkItemAsync(type, item, cancellationToken, late);
        }

        _pendingWebhooks.Clear();
    }

    public async Task<WorkItemDto> ProjectAsync(WorkItem item, CancellationToken cancellationToken)
    {
        var row = await WorkItemQueries
            .Project(db.WorkItems.AsNoTracking().Where(w => w.Id == item.Id))
            .FirstAsync(cancellationToken);

        var directory = await users.MapAsync(
            new[] { row.OwnerUserId }.Concat(row.CompletedByUserId is { } id ? [id] : Array.Empty<Guid>()),
            cancellationToken);

        var progress = await checklists.ForAsync([row.Id], cancellationToken);

        return WorkItemQueries.ToDto(row, directory, clock.UtcNow, progress);
    }

    public async Task ValidateLinksAsync(Guid ownerUserId, Guid? entityId, Guid? departmentId, CancellationToken cancellationToken)
    {
        await users.RequireAssignableAsync(ownerUserId, cancellationToken);

        if (entityId is { } entity && !await db.Entities.AnyAsync(e => e.Id == entity, cancellationToken))
        {
            throw new NotFoundException(ResourceNames.Entity, entity);
        }

        if (departmentId is { } department && !await db.Departments.AnyAsync(d => d.Id == department, cancellationToken))
        {
            throw new NotFoundException(ResourceNames.Department, department);
        }
    }

    /// <summary>Commits without projecting, for the one handler that needs a row id before it can go on.</summary>
    public Task PersistAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public void Add(WorkItem item) => db.WorkItems.Add(item);
}

public sealed class CreateWorkItemHandler(WorkItemMutator mutator) : IRequestHandler<CreateWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(CreateWorkItemCommand request, CancellationToken cancellationToken = default)
    {
        await mutator.ValidateLinksAsync(request.OwnerUserId, request.EntityId, request.DepartmentId, cancellationToken);

        var item = new WorkItem
        {
            Id = Guid.CreateVersion7(),
            ResponsibilityId = null,
            Title = request.Title.Trim(),
            Description = request.Description,
            OwnerUserId = request.OwnerUserId,
            EntityId = request.EntityId,
            DepartmentId = request.DepartmentId,
            DueDate = request.DueDate,
            Status = WorkItemStatus.Open,
            CreatedAt = mutator.Now,
        };

        mutator.Add(item);
        await mutator.PersistAsync(cancellationToken);

        mutator.Record(item, WorkItemEventFactory.Created(
            item,
            mutator.ActorId,
            mutator.Now,
            new { source = WorkItemSources.OneOff }));

        // Giving somebody work is the one moment they most need to hear about it.
        await mutator.NotifyOwnerAsync(item, NotificationType.Assigned, cancellationToken);

        return await mutator.SaveAsync(item, cancellationToken);
    }
}

public sealed class UpdateWorkItemHandler(WorkItemMutator mutator) : IRequestHandler<UpdateWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(UpdateWorkItemCommand request, CancellationToken cancellationToken = default)
    {
        var item = await mutator.LoadAsync(request.Id, cancellationToken);

        await mutator.ValidateLinksAsync(request.OwnerUserId, request.EntityId, request.DepartmentId, cancellationToken);

        var title = request.Title.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        // Anyone signed in may edit anyone's work: in a team this size, correcting a colleague's
        // typo or handing a task over is ordinary cover, not a privilege. What makes that safe is
        // that every change is attributed — so the diff is computed before anything is overwritten.
        var changes = new FieldChangeSet()
            .Track(WorkItemFields.Title, item.Title, title)
            .Track(WorkItemFields.Description, item.Description, description)
            .Track(WorkItemFields.Owner, item.OwnerUserId, request.OwnerUserId)
            .Track(WorkItemFields.Entity, item.EntityId, request.EntityId)
            .Track(WorkItemFields.Department, item.DepartmentId, request.DepartmentId);

        if (!changes.Any)
        {
            // A save that changed nothing is not history; leaving it out keeps the drawer readable.
            return await mutator.ProjectAsync(item, cancellationToken);
        }

        var previousOwner = item.OwnerUserId;

        item.Title = title;
        item.Description = description;
        item.OwnerUserId = request.OwnerUserId;
        item.EntityId = request.EntityId;
        item.DepartmentId = request.DepartmentId;

        // The factory types this as Reassigned when the diff moved the owner, so a hand-over made
        // through the ordinary edit form is queryable as one.
        mutator.Record(item, WorkItemEventFactory.Updated(item, mutator.ActorId, mutator.Now, changes.Changes));

        if (previousOwner != item.OwnerUserId)
        {
            await mutator.NotifyOwnerAsync(item, NotificationType.Assigned, cancellationToken);
        }

        return await mutator.SaveAsync(item, cancellationToken);
    }
}

public sealed class StartWorkItemHandler(WorkItemMutator mutator) : IRequestHandler<StartWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(StartWorkItemCommand request, CancellationToken cancellationToken = default)
    {
        var item = await mutator.LoadAsync(request.Id, cancellationToken);

        mutator.Transition(item, WorkItemStatus.InProgress);

        // Starting work releases a hold: you are no longer waiting on anyone.
        item.ClearHold();

        return await mutator.SaveAsync(item, cancellationToken);
    }
}

public sealed class CompleteWorkItemHandler(WorkItemMutator mutator, CompletionPreconditions preconditions)
    : IRequestHandler<CompleteWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(CompleteWorkItemCommand request, CancellationToken cancellationToken = default)
    {
        var item = await mutator.LoadAsync(request.Id, cancellationToken);

        // Required checks ticked, proof attached — whichever of the two the responsibility asks for.
        // Checked before the transition so a refusal leaves the item exactly as it was.
        await preconditions.EnsureCompletableAsync(item, cancellationToken);

        // A missed item is still workable; completing it records CompletedLate and the miss stands.
        // So does an occurrence whose period ended before the engine's next tick got to it.
        var target = StatusTransitions.CompletionTargetFor(item.Status, item.PeriodEnd, mutator.Now);
        mutator.Transition(item, target);

        item.CompletedAt = mutator.Now;
        item.CompletedByUserId = mutator.ActorId;
        item.ClearHold();

        return await mutator.SaveAsync(item, cancellationToken);
    }
}

public sealed class HoldWorkItemHandler(WorkItemMutator mutator) : IRequestHandler<HoldWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(HoldWorkItemCommand request, CancellationToken cancellationToken = default)
    {
        var reason = request.Reason ?? throw new ValidationException(new Dictionary<string, string[]>
        {
            ["reason"] = ["A hold reason is required."],
        });

        if (reason == HoldReason.Other && string.IsNullOrWhiteSpace(request.Text))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["text"] = ["Free text is required when the hold reason is 'Other'."],
            });
        }

        var item = await mutator.LoadAsync(request.Id, cancellationToken);

        mutator.Transition(item, WorkItemStatus.OnHold, new { reason = reason.ToString(), text = request.Text });

        item.HoldReason = reason;
        item.HoldReasonText = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim();

        // Somebody else parking your work is exactly the kind of thing you find out about a week
        // late otherwise. Parking your own needs no announcement.
        await mutator.NotifyOwnerAsync(
            item,
            NotificationType.PutOnHold,
            cancellationToken,
            (NotificationData.Reason, reason.ToString()));

        return await mutator.SaveAsync(item, cancellationToken);
    }
}

public sealed class ReopenWorkItemHandler(WorkItemMutator mutator) : IRequestHandler<ReopenWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(ReopenWorkItemCommand request, CancellationToken cancellationToken = default)
    {
        var item = await mutator.LoadAsync(request.Id, cancellationToken);

        // Undoing someone else's completion is an owner/admin action; releasing a hold is not.
        if (item.Status.IsCompletion())
        {
            mutator.RequireOwnerOrAdmin(item, "reopen");
        }

        mutator.Transition(item, WorkItemStatus.Open);

        item.CompletedAt = null;
        item.CompletedByUserId = null;
        item.ClearHold();

        return await mutator.SaveAsync(item, cancellationToken);
    }
}

public sealed class CancelWorkItemHandler(WorkItemMutator mutator) : IRequestHandler<CancelWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(CancelWorkItemCommand request, CancellationToken cancellationToken = default)
    {
        var item = await mutator.LoadAsync(request.Id, cancellationToken);

        if (item.IsOccurrence)
        {
            throw new ConflictException(
                "Occurrences cannot be cancelled. Pause or deactivate the responsibility instead.");
        }

        mutator.RequireOwnerOrAdmin(item, "cancel");
        mutator.Transition(item, WorkItemStatus.Cancelled);

        return await mutator.SaveAsync(item, cancellationToken);
    }
}

public sealed class RescheduleWorkItemHandler(WorkItemMutator mutator) : IRequestHandler<RescheduleWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> Handle(RescheduleWorkItemCommand request, CancellationToken cancellationToken = default)
    {
        var item = await mutator.LoadAsync(request.Id, cancellationToken);

        if (!item.Status.IsOutstanding())
        {
            throw new ConflictException($"Only outstanding items can be rescheduled (this one is {item.Status}).");
        }

        // Rescheduling is an action, not a status: it moves the due date and leaves the item exactly
        // as workable as it was. An occurrence may only move inside its own period — otherwise a
        // responsibility could be pushed past the point where its successor spawns and the miss
        // would quietly disappear, which is the behaviour Everdue exists to prevent.
        if (item.IsOccurrence)
        {
            if (item.PeriodEnd is { } periodEnd && request.NewDueDate >= periodEnd)
            {
                throw new ValidationException(
                    "An occurrence can only be rescheduled inside its own period; the new due date must fall before the period ends.");
            }

            if (item.PeriodStart is { } periodStart && request.NewDueDate < periodStart)
            {
                throw new ValidationException("The new due date must fall on or after the start of the occurrence's period.");
            }
        }

        var previous = item.DueDate;
        item.DueDate = request.NewDueDate;

        mutator.Record(item, WorkItemEventFactory.Rescheduled(
            item,
            mutator.ActorId,
            mutator.Now,
            previous,
            request.NewDueDate,
            string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()));

        return await mutator.SaveAsync(item, cancellationToken);
    }
}
