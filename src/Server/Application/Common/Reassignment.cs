using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Common;

/// <summary>
/// The one place an owner change is written, so a hand-over looks identical whether it came from an
/// edit, a bulk action, a responsibility reassignment or a departure.
/// </summary>
public static class Reassignment
{
    /// <summary>
    /// Moves an item and records it as a <see cref="WorkItemEventType.Reassigned"/> event carrying the
    /// same field-diff payload an ordinary edit writes — the old owner included, because "who moved
    /// this off my plate, and when" is the question the table exists to answer.
    /// </summary>
    public static async Task ApplyAsync(
        IEverdueDbContext db,
        IWebhookPublisher webhooks,
        WorkItem item,
        Guid newOwnerUserId,
        Guid actorUserId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var changes = new FieldChangeSet().Track(WorkItemFields.Owner, item.OwnerUserId, newOwnerUserId);

        if (!changes.Any)
        {
            return;
        }

        item.OwnerUserId = newOwnerUserId;
        db.WorkItemEvents.Add(WorkItemEventFactory.Reassigned(item, actorUserId, at, changes.Changes));

        // A hand-over made in bulk raises the same webhook a hand-over made one at a time does; the
        // caller's SaveChanges commits the delivery with the change.
        await webhooks.PublishWorkItemAsync(WebhookEventType.WorkItemReassigned, item, cancellationToken);
    }

    public static NotificationRequest Notify(WorkItem item, Guid newOwnerUserId, Guid actorUserId, string actorDisplayName)
        => new(
            newOwnerUserId,
            NotificationType.Assigned,
            item.Id,
            Data: NotificationData.For(
                (NotificationData.Title, item.Title),
                (NotificationData.Actor, actorDisplayName)));
}
