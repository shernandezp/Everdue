using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Notifications;

public sealed record ListNotificationsQuery(bool UnreadOnly = false, int? Page = null, int? PageSize = null)
    : IQuery<PagedResult<NotificationDto>>;

public sealed record UnreadNotificationCountQuery : IQuery<UnreadCountDto>;

/// <summary>Absent ids means "everything I have not read" — the bell's "mark all read".</summary>
public sealed record MarkNotificationsReadCommand(IReadOnlyList<Guid>? Ids = null) : ICommand<UnreadCountDto>;

public sealed record GetNotificationPreferencesQuery : IQuery<NotificationPreferencesDto>;

public sealed record UpdateNotificationPreferencesCommand(
    NotificationChannel? Channel,
    IReadOnlyDictionary<string, bool>? Types) : ICommand<NotificationPreferencesDto>;

public sealed record StartTelegramLinkCommand : ICommand<TelegramLinkDto>;

public sealed record UnlinkTelegramCommand : ICommand<NotificationPreferencesDto>;

public sealed record ListDigestSubscriptionsQuery : IQuery<IReadOnlyList<DigestSubscriptionDto>>;

/// <summary>
/// Upsert rather than create/update: there is exactly one subscription per person, and a screen with
/// a "create" and an "edit" for a single row is a screen that can get them out of step.
/// </summary>
public sealed record SaveDigestSubscriptionCommand(
    DigestFrequency Frequency,
    DayOfWeek WeeklyDayOfWeek,
    Guid? DepartmentId,
    bool Active) : ICommand<DigestSubscriptionDto>;
