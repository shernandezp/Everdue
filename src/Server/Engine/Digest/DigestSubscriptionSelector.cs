using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Engine.Digest;

/// <summary>
/// Who should receive a digest right now, and how they want it.
///
/// A subscription row is optional: an **active administrator with no row is a daily, org-wide
/// subscriber**, which is exactly what v1 did. That is what makes an upgrade seamless without a data
/// migration, and it means deleting a row cannot resurrect itself into a different meaning — turning
/// the digest off is a row with Active = false, not the absence of one.
/// </summary>
public sealed record DueSubscriber(UserSummary User, DigestSubscription? Subscription)
{
    public DigestFrequency Frequency => Subscription?.Frequency ?? DigestFrequency.Daily;

    public Guid? DepartmentId => Subscription?.DepartmentId;
}

public sealed class DigestSubscriptionSelector(EverdueDbContext db, IUserDirectory users)
{
    public async Task<IReadOnlyList<DueSubscriber>> SelectDueAsync(DateOnly localDate, CancellationToken cancellationToken)
    {
        var subscriptions = await db.DigestSubscriptions.ToListAsync(cancellationToken);
        var byUser = subscriptions.ToDictionary(s => s.UserId);

        var candidates = (await users.ListAsync(includeInactive: false, cancellationToken))
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .ToArray();

        var due = new List<DueSubscriber>();

        foreach (var user in candidates)
        {
            if (byUser.TryGetValue(user.Id, out var subscription))
            {
                if (subscription.IsDueOn(localDate))
                {
                    due.Add(new DueSubscriber(user, subscription));
                }

                continue;
            }

            // No row: administrators are implicit daily subscribers, members are not. A member who
            // wants the digest creates a subscription; that is the difference between opt-out and
            // opt-in, and it is drawn where v1 drew it.
            if (user.Role == UserRole.Admin)
            {
                due.Add(new DueSubscriber(user, null));
            }
        }

        return due;
    }

    /// <summary>
    /// Records that this person has had today's digest. An implicit subscriber is materialised on
    /// first send — the guard has to live somewhere, and inventing a second place to store it would
    /// be one more thing that can disagree.
    /// </summary>
    public async Task MarkSentAsync(DueSubscriber subscriber, DateOnly localDate, CancellationToken cancellationToken)
    {
        if (subscriber.Subscription is { } existing)
        {
            existing.LastSentLocalDate = localDate;
        }
        else
        {
            db.DigestSubscriptions.Add(new DigestSubscription
            {
                Id = Guid.CreateVersion7(),
                UserId = subscriber.User.Id,
                Frequency = DigestFrequency.Daily,
                Active = true,
                LastSentLocalDate = localDate,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
