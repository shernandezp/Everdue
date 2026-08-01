using System.Net;
using System.Text;
using Everdue.Server.Application.Webhooks;
using Everdue.Server.Domain;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Infrastructure.Webhooks;

/// <summary>The outcome of one POST, in the vocabulary the dispatcher acts on.</summary>
public sealed record WebhookSendResult(bool Sent, bool Retryable, int? StatusCode, string? Error);

/// <summary>
/// One HTTP POST, signed.
///
/// <para><strong>What is retryable:</strong> 408, 429, any 5xx, a timeout and a refused connection — things that
/// might be different in a minute. Every other 4xx fails immediately: retrying a 404 or a 401 for an hour is
/// how an outbox becomes log spam.</para>
///
/// <para><strong>Redirects are not followed</strong> and only the first 2 KB of the response body is read, for
/// the error message. A receiver's reply is not input to anything.</para>
/// </summary>
public sealed class WebhookSender(HttpClient client, WebhookSecretProtector secrets, IOptions<WebhookOptions> options)
{
    private const int MaxErrorBytes = 2048;

    public async Task<WebhookSendResult> SendAsync(
        WebhookSubscription subscription,
        WebhookDelivery delivery,
        CancellationToken cancellationToken)
    {
        var secret = secrets.TryUnprotect(subscription.SecretProtected);

        if (secret is null)
        {
            // The key ring is gone, so nothing can be signed for this subscription. Not retryable: it will not
            // start working on its own, and the administrator has to regenerate the secret.
            return new WebhookSendResult(false, false, null, "The signing secret could not be decrypted. Regenerate it.");
        }

        var timestamp = DateTimeOffset.UtcNow;

        using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
        {
            Content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json"),
        };

        request.Headers.TryAddWithoutValidation(WebhookSignature.IdHeader, delivery.EventId.ToString());
        request.Headers.TryAddWithoutValidation(WebhookSignature.TimestampHeader, timestamp.ToUnixTimeSeconds().ToString());
        request.Headers.TryAddWithoutValidation(
            WebhookSignature.SignatureHeader,
            WebhookSignature.Sign(secret, delivery.EventId, timestamp, delivery.PayloadJson));

        request.Headers.TryAddWithoutValidation("user-agent", "Everdue-Webhooks/1.0");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (response.IsSuccessStatusCode)
            {
                return new WebhookSendResult(true, false, (int)response.StatusCode, null);
            }

            var body = await ReadShortAsync(response, timeout.Token);
            var status = (int)response.StatusCode;

            var retryable = status is 408 or 429 || status >= 500;

            return new WebhookSendResult(false, retryable, status, $"HTTP {status}. {body}".Trim());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WebhookSendResult(false, true, null, $"Timed out after {options.Value.TimeoutSeconds}s.");
        }
        catch (HttpRequestException e)
        {
            // DNS, TLS and refused connections all land here. All of them might be different next time.
            return new WebhookSendResult(false, true, e.StatusCode is { } code ? (int)code : null, e.Message);
        }
    }

    private static async Task<string> ReadShortAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var buffer = new byte[MaxErrorBytes];
            var read = await stream.ReadAsync(buffer, cancellationToken);

            return Encoding.UTF8.GetString(buffer, 0, read).Trim();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Redirects are not followed and cookies are not stored: the URL an administrator typed is the only place
    /// a payload goes.
    /// </summary>
    public static void Configure(HttpClient http) => http.Timeout = Timeout.InfiniteTimeSpan;

    public static HttpClientHandler Handler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None,
    };
}
