using Everdue.Server.Domain;

namespace Everdue.Server.Application.Abstractions;

/// <summary>
/// Everything needed to decide whether and where to tell somebody. Distinct from
/// <see cref="UserSummary"/> on purpose: "who may own work" and "how do I reach this person" are
/// different questions, and the second one carries addresses the rest of the app has no business
/// seeing.
/// </summary>
public sealed record NotificationRecipient(
    Guid UserId,
    string DisplayName,
    string? Email,
    string Language,
    NotificationPreferences Preferences,
    long? TelegramChatId,
    string? WhatsAppPhoneE164,
    bool Active)
{
    public ChannelRecipient ToChannelRecipient()
        => new(UserId, DisplayName, Email, TelegramChatId, WhatsAppPhoneE164, Language);

    /// <summary>Does this person have an address on the channel they chose?</summary>
    public bool CanReceiveOn(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Email => !string.IsNullOrWhiteSpace(Email),
        NotificationChannel.Telegram => TelegramChatId is not null,
        NotificationChannel.WhatsApp => !string.IsNullOrWhiteSpace(WhatsAppPhoneE164),
        _ => false,
    };
}

public interface INotificationRecipients
{
    Task<NotificationRecipient?> FindAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, NotificationRecipient>> MapAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task SavePreferencesAsync(Guid userId, NotificationPreferences preferences, CancellationToken cancellationToken = default);
}
