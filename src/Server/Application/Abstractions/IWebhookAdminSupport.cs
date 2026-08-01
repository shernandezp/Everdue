using Everdue.Server.Domain;

namespace Everdue.Server.Application.Abstractions;

/// <summary>
/// Everything Infrastructure has to supply for a webhook subscription to be managed: a URL rule, a signing secret,
/// and a way to queue the test event.
///
/// Declared here rather than beside the handlers so the Application layer manages subscriptions without ever
/// learning that Data Protection, an <c>HttpClient</c> or a URL policy exist.
/// </summary>
public interface IWebhookAdminSupport
{
    /// <summary>Throws a <c>ValidationException</c> for a URL that may not receive payloads.</summary>
    void ValidateUrl(string url);

    /// <summary>A new signing secret plus the ciphertext to store. Shown to the administrator exactly once.</summary>
    (string Secret, string Protected) NewSecret();

    /// <summary>Adds one <c>ping</c> delivery. Committed by the caller's save, like every other enqueue.</summary>
    void EnqueuePing(WebhookSubscription subscription);
}
