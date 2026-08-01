namespace Everdue.Server.Domain;

/// <summary>
/// The five things worth interrupting somebody for. Deliberately closed: every addition is a new
/// reason for the tool to speak, and a tool that speaks too often gets muted.
/// </summary>
public enum NotificationType
{
    Assigned = 0,
    DueToday = 1,
    Missed = 2,
    Mentioned = 3,
    PutOnHold = 4,
}

/// <summary>
/// Where a notification can be delivered *outside* the app. In-app is not a channel: it is the
/// notification row itself, which is why it always works and never has a delivery record.
/// </summary>
public enum NotificationChannel
{
    Email = 0,
    Telegram = 1,
    WhatsApp = 2,
}

public enum DeliveryStatus
{
    Pending = 0,
    Sent = 1,

    /// <summary>Gave up: either the attempt cap was reached or the provider said "never".</summary>
    Failed = 2,

    /// <summary>The channel was not configured or the user has no address on it. Not an error.</summary>
    Skipped = 3,
}

public enum DigestFrequency
{
    Daily = 0,
    Weekly = 1,
}

public static class NotificationTypes
{
    /// <summary>
    /// Read off the enum rather than restated, so a sixth notification type cannot ship with a
    /// preferences screen that silently omits it.
    /// </summary>
    public static readonly IReadOnlyList<NotificationType> All = Enum.GetValues<NotificationType>();
}
