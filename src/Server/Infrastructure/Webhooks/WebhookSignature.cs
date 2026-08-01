using System.Security.Cryptography;
using System.Text;

namespace Everdue.Server.Infrastructure.Webhooks;

/// <summary>
/// Signs a delivery to the <a href="https://www.standardwebhooks.com">Standard Webhooks</a> specification, so a
/// subscriber can verify us with an off-the-shelf library instead of reading our prose:
///
/// <code>
/// webhook-id:        {eventId}
/// webhook-timestamp: {unix seconds}
/// webhook-signature: v1,{base64(HMAC-SHA256(secret, "{id}.{timestamp}.{body}"))}
/// </code>
///
/// The <c>v1</c> prefix is the scheme version, and the header can hold several space-separated signatures during
/// a secret rotation. <c>webhook-id</c> is stable across retries, which is what makes a receiver's
/// deduplication possible — delivery is at-least-once and the documentation says so in those words.
/// </summary>
public static class WebhookSignature
{
    public const string IdHeader = "webhook-id";
    public const string TimestampHeader = "webhook-timestamp";
    public const string SignatureHeader = "webhook-signature";

    /// <summary>32 random bytes, hex, shown to the administrator exactly once.</summary>
    public static string NewSecret() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public static string Sign(string secret, Guid eventId, DateTimeOffset timestamp, string body)
    {
        var signedContent = $"{eventId}.{timestamp.ToUnixTimeSeconds()}.{body}";

        var mac = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(signedContent));

        return $"v1,{Convert.ToBase64String(mac)}";
    }
}
