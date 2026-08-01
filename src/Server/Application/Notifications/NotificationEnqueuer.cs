using System.Text.Json;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Notifications;

/// <summary>
/// Turns "this happened to you" into a notification row plus, when the person has chosen a channel
/// they can actually be reached on, one delivery row.
///
/// Two rules decide whether anything is written at all:
/// 1. the recipient must want this type (a switched-off type produces nothing — not a suppressed
///    delivery, nothing, because an unread bell badge for something you muted is still noise);
/// 2. an existing dedupe key wins. The unique index is the real guarantee; this check is what keeps
///    the common case from turning into a failed transaction that would take the caller's work down
///    with it.
/// </summary>
public sealed class NotificationEnqueuer(IEverdueDbContext db, INotificationRecipients recipients, IClock clock)
    : INotificationEnqueuer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task EnqueueAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        => EnqueueManyAsync([request], cancellationToken);

    public async Task EnqueueManyAsync(IReadOnlyCollection<NotificationRequest> requests, CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var people = await recipients.MapAsync(requests.Select(r => r.UserId).Distinct(), cancellationToken);
        var taken = await ExistingDedupeKeysAsync(requests, cancellationToken);
        var now = clock.UtcNow;

        foreach (var request in requests)
        {
            if (!people.TryGetValue(request.UserId, out var person) || !person.Active)
            {
                continue;
            }

            if (!person.Preferences.IsEnabled(request.Type))
            {
                continue;
            }

            if (request.DedupeKey is { } key && !taken.Add(key))
            {
                continue;
            }

            var notification = new Notification
            {
                Id = Guid.CreateVersion7(),
                UserId = person.UserId,
                Type = request.Type,
                WorkItemId = request.WorkItemId,
                CommentId = request.CommentId,
                DataJson = request.Data is { Count: > 0 } data ? JsonSerializer.Serialize(data, Json) : null,
                DedupeKey = request.DedupeKey,
                CreatedAt = now,
            };

            db.Notifications.Add(notification);

            // In-app already works at this point. A delivery row is only added when the person asked
            // for one *and* we know where to send it — which is why an install with no channels
            // configured produces no pending work and no errors.
            if (person.Preferences.Channel is { } channel && person.CanReceiveOn(channel))
            {
                db.NotificationDeliveries.Add(new NotificationDelivery
                {
                    Id = Guid.CreateVersion7(),
                    NotificationId = notification.Id,
                    Channel = channel,
                    Status = DeliveryStatus.Pending,
                    NextAttemptAt = now,
                });
            }
        }
    }

    private async Task<HashSet<string>> ExistingDedupeKeysAsync(
        IReadOnlyCollection<NotificationRequest> requests,
        CancellationToken cancellationToken)
    {
        var keys = requests
            .Select(r => r.DedupeKey)
            .Where(k => k is not null)
            .Select(k => k!)
            .Distinct()
            .ToArray();

        if (keys.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var existing = await db.Notifications.AsNoTracking()
            .Where(n => n.DedupeKey != null && keys.Contains(n.DedupeKey))
            .Select(n => n.DedupeKey!)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(existing, StringComparer.Ordinal);
    }
}
