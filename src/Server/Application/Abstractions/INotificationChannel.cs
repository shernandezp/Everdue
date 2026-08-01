using Everdue.Server.Domain;

namespace Everdue.Server.Application.Abstractions;

public enum ChannelSendOutcome
{
    Sent = 0,

    /// <summary>The provider might succeed later: a 429, a 5xx, a socket that gave up.</summary>
    RetryableFailure = 1,

    /// <summary>The provider will never succeed with this message: a revoked token, a blocked user, a rejected template.</summary>
    PermanentFailure = 2,

    /// <summary>Not configured, or this person has no address on this channel. Not an error — nothing was owed.</summary>
    Skipped = 3,
}

/// <summary><see cref="RetryAfter"/> is honoured when the provider tells us how long to wait (Telegram does).</summary>
public sealed record ChannelSendResult(ChannelSendOutcome Outcome, string? Error = null, TimeSpan? RetryAfter = null)
{
    public static ChannelSendResult Sent() => new(ChannelSendOutcome.Sent);

    public static ChannelSendResult Retry(string error, TimeSpan? retryAfter = null)
        => new(ChannelSendOutcome.RetryableFailure, Truncate(error), retryAfter);

    public static ChannelSendResult Permanent(string error) => new(ChannelSendOutcome.PermanentFailure, Truncate(error));

    public static ChannelSendResult Skipped(string reason) => new(ChannelSendOutcome.Skipped, Truncate(reason));

    /// <summary>Matches the LastError column, so a provider that returns an essay cannot fail the insert.</summary>
    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];
}

/// <summary>Where a person can be reached. Assembled once per delivery, never guessed at by a channel.</summary>
public sealed record ChannelRecipient(
    Guid UserId,
    string DisplayName,
    string? Email,
    long? TelegramChatId,
    string? WhatsAppPhoneE164,
    string Language);

/// <summary>
/// One message, rendered every way a channel might need it.
///
/// The template fields exist because some providers cannot send free text to somebody who did not
/// message first — WhatsApp business-initiated messages must be a pre-approved template. Carrying
/// both renderings is honest about that; making every channel accept plain text and then quietly
/// failing on one of them would not be.
/// </summary>
public sealed record ChannelMessage(
    string Subject,
    string PlainText,
    string? HtmlBody = null,
    string? TemplateKey = null,
    IReadOnlyList<string>? TemplateArgs = null,
    string Language = Languages.Spanish);

public interface INotificationChannel
{
    NotificationChannel Channel { get; }

    /// <summary>
    /// Can this channel send at all right now?
    ///
    /// Asked of the channel rather than of the settings table because a channel may have more than
    /// one source of configuration — e-mail also honours the appsettings <c>Smtp:*</c> block, and only the
    /// e-mail channel has any business knowing that. Every screen that says "configured" and every
    /// screen that offers a channel to a user asks this one question, so they cannot disagree.
    /// </summary>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    Task<ChannelSendResult> SendAsync(ChannelRecipient recipient, ChannelMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Resolves a channel implementation by enum. The dispatcher and the digest both go through it.</summary>
public interface IChannelRegistry
{
    INotificationChannel? Find(NotificationChannel channel);

    IReadOnlyList<INotificationChannel> All { get; }

    /// <summary>The channels that could actually deliver something today.</summary>
    Task<IReadOnlyList<NotificationChannel>> ConfiguredAsync(CancellationToken cancellationToken = default);
}
