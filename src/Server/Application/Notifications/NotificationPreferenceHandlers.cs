using System.Security.Cryptography;
using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Notifications;

/// <summary>
/// Builds the preferences view. <c>AvailableChannels</c> is what makes the screen honest: a channel
/// nobody has configured is not offered, so a user can never switch to one and then wonder why
/// nothing arrives.
/// </summary>
internal sealed class PreferencesView(INotificationRecipients recipients, IChannelRegistry channels)
{
    public async Task<NotificationPreferencesDto> BuildAsync(Guid userId, CancellationToken cancellationToken)
    {
        var person = await recipients.FindAsync(userId, cancellationToken)
                     ?? throw new NotFoundException(ResourceNames.User, userId);

        // Asked of the channels themselves, so this list and the administrator's "configured" column
        // are the same answer — and so nobody is offered a channel that would silently do nothing.
        var available = await channels.ConfiguredAsync(cancellationToken);

        var types = NotificationTypes.All.ToDictionary(
            type => type.ToString(),
            person.Preferences.IsEnabled);

        return new NotificationPreferencesDto(
            person.Preferences.Channel,
            types,
            person.TelegramChatId is not null,
            person.WhatsAppPhoneE164,
            available);
    }
}

public sealed class GetNotificationPreferencesHandler(
    INotificationRecipients recipients,
    IChannelRegistry channels,
    ICurrentUser currentUser) : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferencesDto>
{
    public Task<NotificationPreferencesDto> Handle(
        GetNotificationPreferencesQuery request,
        CancellationToken cancellationToken = default)
        => new PreferencesView(recipients, channels).BuildAsync(currentUser.RequireUserId(), cancellationToken);
}

public sealed class UpdateNotificationPreferencesHandler(
    INotificationRecipients recipients,
    IChannelRegistry channels,
    ICurrentUser currentUser) : IRequestHandler<UpdateNotificationPreferencesCommand, NotificationPreferencesDto>
{
    public async Task<NotificationPreferencesDto> Handle(
        UpdateNotificationPreferencesCommand request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        var person = await recipients.FindAsync(userId, cancellationToken) ?? throw new NotFoundException(ResourceNames.User, userId);

        var preferences = person.Preferences;
        preferences.Channel = request.Channel;

        if (request.Types is { } types)
        {
            foreach (var type in NotificationTypes.All)
            {
                if (types.TryGetValue(type.ToString(), out var enabled))
                {
                    preferences.SetEnabled(type, enabled);
                }
            }
        }

        // Choosing a channel you have no address on would silently mean "in-app only"; refusing is
        // the difference between a preference and a misunderstanding.
        if (preferences.Channel is { } channel && !person.CanReceiveOn(channel))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["channel"] = [$"You have no {channel} address on your profile yet."],
            });
        }

        await recipients.SavePreferencesAsync(userId, preferences, cancellationToken);
        return await new PreferencesView(recipients, channels).BuildAsync(userId, cancellationToken);
    }
}

/// <summary>
/// Issues the one-time code the user sends to the bot. Short-lived and single-use: it is the only
/// thing standing between a chat id and somebody's account.
/// </summary>
public sealed class StartTelegramLinkHandler(
    IChannelSettingsResolver resolver,
    ITelegramLinkStore links,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<StartTelegramLinkCommand, TelegramLinkDto>
{
    /// <summary>Unambiguous alphabet — no O/0 or I/1 — because this gets read off a screen and typed on a phone.</summary>
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public async Task<TelegramLinkDto> Handle(StartTelegramLinkCommand request, CancellationToken cancellationToken = default)
    {
        var config = (await resolver.ResolveAsync(NotificationChannel.Telegram, cancellationToken))
            ?.Read<TelegramChannelConfig>();

        if (config is not { IsUsable: true })
        {
            throw new ValidationException("Telegram is not configured on this Everdue installation.");
        }

        var code = RandomCode();
        var expiresAt = clock.UtcNow.Add(Lifetime);

        await links.IssueAsync(currentUser.RequireUserId(), code, expiresAt, cancellationToken);

        var deepLink = string.IsNullOrWhiteSpace(config.BotUsername)
            ? null
            : $"https://t.me/{config.BotUsername.TrimStart('@')}?start={code}";

        return new TelegramLinkDto(code, config.BotUsername, deepLink, expiresAt);
    }

    private static string RandomCode()
        => new(Enumerable.Range(0, 8).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());
}

public sealed class UnlinkTelegramHandler(
    INotificationRecipients recipients,
    IChannelRegistry channels,
    ITelegramLinkStore links,
    ICurrentUser currentUser) : IRequestHandler<UnlinkTelegramCommand, NotificationPreferencesDto>
{
    public async Task<NotificationPreferencesDto> Handle(UnlinkTelegramCommand request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();
        await links.UnlinkAsync(userId, cancellationToken);

        // Leaving the channel selected would mean every future notification resolves to "no address"
        // and quietly becomes in-app only.
        var person = await recipients.FindAsync(userId, cancellationToken) ?? throw new NotFoundException(ResourceNames.User, userId);

        if (person.Preferences.Channel == NotificationChannel.Telegram)
        {
            person.Preferences.Channel = null;
            await recipients.SavePreferencesAsync(userId, person.Preferences, cancellationToken);
        }

        return await new PreferencesView(recipients, channels).BuildAsync(userId, cancellationToken);
    }
}
