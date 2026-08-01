using Everdue.Server.Application.Abstractions;
using Everdue.Server.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Everdue.Server.Infrastructure.Email;

/// <summary>
/// The digest is the only thing that sends mail, and it is optional: with no SMTP host configured
/// this logs a warning and drops the message. Nothing else in the system depends on e-mail.
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning(
                "SMTP is not configured (Smtp:Host / Smtp:From); dropping e-mail '{Subject}' to {Recipient}.",
                message.Subject,
                message.ToAddress);
            return;
        }

        var host = _options.Host!;
        var from = _options.From!;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName ?? "Everdue", from));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var secureSocketOptions = _options.UseStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.Auto;

        await client.ConnectAsync(host, _options.Port, secureSocketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.User))
        {
            await client.AuthenticateAsync(_options.User, _options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Sent '{Subject}' to {Recipient}.", message.Subject, message.ToAddress);
    }
}
