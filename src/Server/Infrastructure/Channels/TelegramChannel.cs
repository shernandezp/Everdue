using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Infrastructure.Channels;

/// <summary>
/// The channel that carries the version's promise: free, no template approval, no verification, and
/// it reaches somebody who never opens a laptop.
/// </summary>
public sealed class TelegramChannel(
    IChannelSettingsResolver resolver,
    TelegramApiClient api,
    EverdueDbContext db) : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.Telegram;

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
        => (await resolver.ResolveAsync(NotificationChannel.Telegram, cancellationToken))
            ?.Read<TelegramChannelConfig>() is { IsUsable: true };

    public async Task<ChannelSendResult> SendAsync(
        ChannelRecipient recipient,
        ChannelMessage message,
        CancellationToken cancellationToken = default)
    {
        if (recipient.TelegramChatId is not { } chatId)
        {
            return ChannelSendResult.Skipped("The recipient has not linked Telegram.");
        }

        var config = (await resolver.ResolveAsync(NotificationChannel.Telegram, cancellationToken))
            ?.Read<TelegramChannelConfig>();

        if (config is not { IsUsable: true })
        {
            return ChannelSendResult.Skipped("Telegram is not configured.");
        }

        var result = await api.SendMessageAsync(config.BotToken!, chatId, message.PlainText, cancellationToken);

        if (result.Ok)
        {
            return ChannelSendResult.Sent();
        }

        // Blocking the bot is a decision, not a fault: forget the chat id so this person stops
        // producing deliveries nobody will ever read.
        if (result.ChatUnreachable)
        {
            await ClearChatIdAsync(recipient.UserId, cancellationToken);
        }

        return result.Retryable
            ? ChannelSendResult.Retry(result.Error ?? "Telegram send failed.", result.RetryAfter)
            : ChannelSendResult.Permanent(result.Error ?? "Telegram refused the message.");
    }

    private async Task ClearChatIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user?.TelegramChatId is null)
        {
            return;
        }

        user.TelegramChatId = null;
        await db.SaveChangesAsync(cancellationToken);
    }
}
