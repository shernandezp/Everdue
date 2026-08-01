using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Everdue.Server.Infrastructure.Channels;

/// <summary>
/// SMTP as a channel.
///
/// Configuration comes from a ChannelSettings row when one exists, and otherwise from the v1
/// <c>Smtp:*</c> appsettings block. That fallback is the whole upgrade story for existing installs:
/// they keep sending exactly as they did, with nothing to configure and no migration to run.
/// </summary>
public sealed class EmailChannel(
    IChannelSettingsResolver resolver,
    IOptions<SmtpOptions> fallback,
    ILogger<EmailChannel> logger) : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.Email;

    /// <summary>
    /// Includes the appsettings fallback, which is the whole reason this question belongs to the
    /// channel: an install that has been sending mail since v1 has no ChannelSettings row, and a
    /// screen that asked the settings table alone would tell it e-mail is not configured.
    /// </summary>
    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
        => await ResolveConfigAsync(cancellationToken) is not null;

    public async Task<ChannelSendResult> SendAsync(
        ChannelRecipient recipient,
        ChannelMessage message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipient.Email))
        {
            return ChannelSendResult.Skipped("The recipient has no e-mail address.");
        }

        var config = await ResolveConfigAsync(cancellationToken);
        if (config is null)
        {
            return ChannelSendResult.Skipped("SMTP is not configured.");
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(config.FromName ?? "Everdue", config.From!));
        mime.To.Add(new MailboxAddress(recipient.DisplayName, recipient.Email));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody ?? $"<p>{System.Net.WebUtility.HtmlEncode(message.PlainText)}</p>",
            TextBody = message.PlainText,
        }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            var socketOptions = config.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.Auto;

            await client.ConnectAsync(config.Host!, config.Port, socketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(config.User))
            {
                await client.AuthenticateAsync(config.User, config.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            logger.LogInformation("Sent '{Subject}' to {Recipient}.", message.Subject, recipient.Email);
            return ChannelSendResult.Sent();
        }
        catch (AuthenticationException e)
        {
            // Wrong credentials will be wrong again in five minutes.
            return ChannelSendResult.Permanent($"SMTP authentication failed: {e.Message}");
        }
        catch (SmtpCommandException e) when (e.StatusCode is SmtpStatusCode.MailboxUnavailable or SmtpStatusCode.MailboxNameNotAllowed)
        {
            return ChannelSendResult.Permanent($"SMTP rejected the recipient: {e.Message}");
        }
        catch (Exception e)
        {
            return ChannelSendResult.Retry($"SMTP send failed: {e.Message}");
        }
    }

    /// <summary>Row first, appsettings second — the resolution order the rest of the system uses, plus v1's block.</summary>
    private async Task<SmtpChannelConfig?> ResolveConfigAsync(CancellationToken cancellationToken)
    {
        if (await resolver.ResolveAsync(NotificationChannel.Email, cancellationToken) is { } resolved
            && resolved.Read<SmtpChannelConfig>() is { IsUsable: true } fromRow)
        {
            return fromRow;
        }

        var options = fallback.Value;
        if (!options.IsConfigured)
        {
            return null;
        }

        // The appsettings block is the operator's mail server — system-scope credentials by another
        // name — so the same flag governs it. Otherwise a tenant told to bring its own would still
        // be quietly sending through the host's.
        if (!await resolver.CanUseSystemChannelsAsync(cancellationToken))
        {
            return null;
        }

        return new SmtpChannelConfig
        {
            Host = options.Host,
            Port = options.Port,
            User = options.User,
            Password = options.Password,
            From = options.From,
            FromName = options.FromName,
            UseStartTls = options.UseStartTls,
        };
    }
}
