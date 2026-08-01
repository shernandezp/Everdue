using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Notifications;

/// <summary>
/// Administrators see every subscription (it is a distribution list, and somebody has to be able to
/// tell who is getting mail); everyone else sees their own.
/// </summary>
public sealed class ListDigestSubscriptionsHandler(IEverdueDbContext db, ICurrentUser currentUser, IUserDirectory users)
    : IRequestHandler<ListDigestSubscriptionsQuery, IReadOnlyList<DigestSubscriptionDto>>
{
    public async Task<IReadOnlyList<DigestSubscriptionDto>> Handle(
        ListDigestSubscriptionsQuery request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var query = db.DigestSubscriptions.AsNoTracking();
        if (!currentUser.IsAdmin)
        {
            query = query.Where(s => s.UserId == userId);
        }

        var rows = await query
            .Select(s => new
            {
                s.Id,
                s.UserId,
                s.Frequency,
                s.WeeklyDayOfWeek,
                s.DepartmentId,
                DepartmentName = s.DepartmentId == null ? null : s.Department!.Name,
                s.Active,
                s.LastSentLocalDate,
            })
            .ToListAsync(cancellationToken);

        var directory = await users.MapAsync(rows.Select(r => r.UserId), cancellationToken);

        return rows
            .Select(r => new DigestSubscriptionDto(
                r.Id,
                r.UserId,
                directory.TryGetValue(r.UserId, out var user) ? user.DisplayName : "—",
                r.Frequency,
                r.WeeklyDayOfWeek,
                r.DepartmentId,
                r.DepartmentName,
                r.Active,
                r.LastSentLocalDate))
            .OrderBy(r => r.UserDisplayName)
            .ToArray();
    }
}

/// <summary>
/// Upserts the caller's own subscription. Materialising the row is also how an implicit
/// administrator subscriber becomes explicit — from here on their preference is recorded, not assumed.
/// </summary>
public sealed class SaveDigestSubscriptionHandler(IEverdueDbContext db, ICurrentUser currentUser, IUserDirectory users)
    : IRequestHandler<SaveDigestSubscriptionCommand, DigestSubscriptionDto>
{
    public async Task<DigestSubscriptionDto> Handle(
        SaveDigestSubscriptionCommand request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        if (request.DepartmentId is { } departmentId
            && !await db.Departments.AnyAsync(d => d.Id == departmentId, cancellationToken))
        {
            throw new NotFoundException(ResourceNames.Department, departmentId);
        }

        var subscription = await db.DigestSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (subscription is null)
        {
            subscription = new DigestSubscription { Id = Guid.CreateVersion7(), UserId = userId };
            db.DigestSubscriptions.Add(subscription);
        }

        subscription.Frequency = request.Frequency;
        subscription.WeeklyDayOfWeek = request.WeeklyDayOfWeek;
        subscription.DepartmentId = request.DepartmentId;
        subscription.Active = request.Active;

        await db.SaveChangesAsync(cancellationToken);

        var departmentName = subscription.DepartmentId is null
            ? null
            : await db.Departments.Where(d => d.Id == subscription.DepartmentId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var user = await users.FindAsync(userId, cancellationToken);

        return new DigestSubscriptionDto(
            subscription.Id,
            userId,
            user?.DisplayName ?? "—",
            subscription.Frequency,
            subscription.WeeklyDayOfWeek,
            subscription.DepartmentId,
            departmentName,
            subscription.Active,
            subscription.LastSentLocalDate);
    }
}
