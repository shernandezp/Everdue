using Everdue.Server.Application.Common;
using Everdue.Server.Application.Webhooks;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Infrastructure.Webhooks;

/// <summary>
/// What counts as an acceptable receiver URL.
///
/// <para><strong>HTTPS is required</strong> unless <c>Webhooks:AllowInsecureUrls</c> says otherwise, because the
/// payload names people's work and the signature does not encrypt it.</para>
///
/// <para><strong>Private and loopback addresses are allowed.</strong> This is a deliberate departure from the
/// usual SSRF advice, and it is the right one here: a self-hosted Everdue posting to an automation box on the
/// same LAN <em>is</em> the use case, only an administrator can create a subscription, and that administrator
/// can already read everything the payload would carry. What is closed instead is the thing that turns a URL
/// into a probe of somewhere else: redirects are not followed, and the response body is discarded beyond the
/// few bytes kept for an error message.</para>
/// </summary>
public sealed class WebhookUrlPolicy(IOptions<WebhookOptions> options)
{
    public Uri Validate(string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["url"] = ["Enter an absolute URL, for example https://example.com/everdue."],
            });
        }

        var isHttps = uri.Scheme == Uri.UriSchemeHttps;
        var isHttp = uri.Scheme == Uri.UriSchemeHttp;

        if (!isHttps && !isHttp)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["url"] = ["Only http and https URLs can receive webhooks."],
            });
        }

        if (isHttp && !options.Value.AllowInsecureUrls)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["url"] = ["Use https. Set Webhooks:AllowInsecureUrls to allow a plain-http receiver."],
            });
        }

        return uri;
    }
}
