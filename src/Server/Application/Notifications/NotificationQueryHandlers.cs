using System.Text.Json;
using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Notifications;

/// <summary>Reads the bell. Own rows only — the tenant filter handles the rest.</summary>
public sealed class ListNotificationsHandler(IEverdueDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListNotificationsQuery, PagedResult<NotificationDto>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PagedResult<NotificationDto>> Handle(
        ListNotificationsQuery request,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);
        var userId = currentUser.RequireUserId();

        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        if (request.UnreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.WorkItemId,
                n.CommentId,
                n.DataJson,
                n.CreatedAt,
                n.ReadAt,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new NotificationDto(
                r.Id,
                r.Type,
                r.WorkItemId,
                r.CommentId,
                ReadData(r.DataJson),
                r.CreatedAt,
                r.ReadAt))
            .ToArray();

        return new PagedResult<NotificationDto>(items, total, page, pageSize);
    }

    private static IReadOnlyDictionary<string, string?> ReadData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            // The payload is free-form by design; a shape this build does not know is not an error.
            return new Dictionary<string, string?>();
        }
    }
}

public sealed class UnreadNotificationCountHandler(IEverdueDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UnreadNotificationCountQuery, UnreadCountDto>
{
    public async Task<UnreadCountDto> Handle(UnreadNotificationCountQuery request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var unread = await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);
        return new UnreadCountDto(unread);
    }
}

public sealed class MarkNotificationsReadHandler(IEverdueDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<MarkNotificationsReadCommand, UnreadCountDto>
{
    public async Task<UnreadCountDto> Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var query = db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null);

        if (request.Ids is { Count: > 0 } ids)
        {
            query = query.Where(n => ids.Contains(n.Id));
        }

        var unread = await query.ToListAsync(cancellationToken);
        var now = clock.UtcNow;

        foreach (var notification in unread)
        {
            notification.ReadAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        var remaining = await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);
        return new UnreadCountDto(remaining);
    }
}
