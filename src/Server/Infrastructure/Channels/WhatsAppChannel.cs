using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;

namespace Everdue.Server.Infrastructure.Channels;

/// <summary>
/// WhatsApp, business-initiated: always a pre-approved template, never free text.
///
/// A notification type with no template name configured is **skipped, not failed** — an approval that
/// has not landed yet should leave the person on their other channel, not produce a red row in the
/// health screen every time something happens.
/// </summary>
public sealed class WhatsAppChannel(IChannelSettingsResolver resolver, WhatsAppCloudApiClient api) : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.WhatsApp;

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
        => (await resolver.ResolveAsync(NotificationChannel.WhatsApp, cancellationToken))
            ?.Read<WhatsAppChannelConfig>() is { IsUsable: true };

    public async Task<ChannelSendResult> SendAsync(
        ChannelRecipient recipient,
        ChannelMessage message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipient.WhatsAppPhoneE164))
        {
            return ChannelSendResult.Skipped("The recipient has no WhatsApp number.");
        }

        var config = (await resolver.ResolveAsync(NotificationChannel.WhatsApp, cancellationToken))
            ?.Read<WhatsAppChannelConfig>();

        if (config is not { IsUsable: true })
        {
            return ChannelSendResult.Skipped("WhatsApp is not configured.");
        }

        if (message.TemplateKey is null || config.TemplateFor(message.TemplateKey) is not { } templateName)
        {
            return ChannelSendResult.Skipped($"No approved WhatsApp template is configured for '{message.TemplateKey}'.");
        }

        var result = await api.SendTemplateAsync(
            config.PhoneNumberId!,
            config.AccessToken!,
            recipient.WhatsAppPhoneE164,
            templateName,
            ResolveTemplateLanguage(config, message.Language),
            message.TemplateArgs ?? [],
            cancellationToken);

        if (result.Ok)
        {
            return ChannelSendResult.Sent();
        }

        return result.Retryable
            ? ChannelSendResult.Retry(result.Error ?? "WhatsApp send failed.")
            : ChannelSendResult.Permanent(result.Error ?? "WhatsApp refused the message.");
    }

    /// <summary>
    /// Meta's language codes are per-template, not per-user: a template approved as <c>es</c> cannot
    /// be sent as <c>en</c>. The configured code wins, and the recipient's language only chooses the
    /// variable text.
    /// </summary>
    private static string ResolveTemplateLanguage(WhatsAppChannelConfig config, string _)
        => string.IsNullOrWhiteSpace(config.TemplateLanguage) ? "es" : config.TemplateLanguage;
}
