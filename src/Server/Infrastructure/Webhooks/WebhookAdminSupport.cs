using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;

namespace Everdue.Server.Infrastructure.Webhooks;

/// <summary>
/// The three Infrastructure services the webhook admin handlers need, behind one Application-facing interface —
/// so those handlers manage subscriptions without knowing that Data Protection, HttpClient or a URL policy
/// exist.
/// </summary>
public sealed class WebhookAdminSupport(
    WebhookUrlPolicy urls,
    WebhookSecretProtector secrets,
    WebhookPublisher publisher) : IWebhookAdminSupport
{
    public void ValidateUrl(string url) => urls.Validate(url);

    public (string Secret, string Protected) NewSecret()
    {
        var secret = WebhookSignature.NewSecret();
        return (secret, secrets.Protect(secret));
    }

    /// <summary>
    /// Through the publisher, which owns delivery-row construction: the test button is one more caller of it
    /// rather than a second place that knows the shape of the table.
    /// </summary>
    public void EnqueuePing(WebhookSubscription subscription) => publisher.EnqueuePing(subscription);
}
