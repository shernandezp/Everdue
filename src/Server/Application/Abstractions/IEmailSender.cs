namespace Everdue.Server.Application.Abstractions;

public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody);

/// <summary>
/// The only outbound channel in v1, and it is optional: when SMTP is unconfigured the
/// implementation logs a warning and drops the message. Nothing but the digest depends on it.
/// </summary>
public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
