namespace Everdue.Server.Application.Abstractions;

/// <summary>
/// The three writes the Telegram link flow needs on a user row. A tiny abstraction rather than
/// widening <see cref="INotificationRecipients"/>: reading where to reach somebody and changing who
/// they are reachable as are different privileges.
/// </summary>
public interface ITelegramLinkStore
{
    Task IssueAsync(Guid userId, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    Task UnlinkAsync(Guid userId, CancellationToken cancellationToken = default);
}
