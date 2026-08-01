using Everdue.Server.Domain;

namespace Everdue.Server.Application.Contracts;

/// <summary>
/// <paramref name="Data"/> is the render parameter bag, not text: the bell renders it in the
/// reader's UI language through the same i18n catalogue as every other string in the app.
/// </summary>
public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    Guid? WorkItemId,
    Guid? CommentId,
    IReadOnlyDictionary<string, string?> Data,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record UnreadCountDto(int Unread);

public sealed record NotificationPreferencesDto(
    NotificationChannel? Channel,
    IReadOnlyDictionary<string, bool> Types,
    bool TelegramLinked,
    string? WhatsAppPhoneE164,
    IReadOnlyList<NotificationChannel> AvailableChannels);

/// <summary>What the profile screen shows after asking to link: a code, and the link that carries it.</summary>
public sealed record TelegramLinkDto(string Code, string? BotUsername, string? DeepLink, DateTimeOffset ExpiresAt);

public sealed record DigestSubscriptionDto(
    Guid Id,
    Guid UserId,
    string UserDisplayName,
    DigestFrequency Frequency,
    DayOfWeek WeeklyDayOfWeek,
    Guid? DepartmentId,
    string? DepartmentName,
    bool Active,
    DateOnly? LastSentLocalDate);
