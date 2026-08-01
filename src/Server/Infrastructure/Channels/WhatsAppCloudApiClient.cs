using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Everdue.Server.Infrastructure.Channels;

public sealed record WhatsAppError(
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("code")] int? Code,
    [property: JsonPropertyName("error_subcode")] int? Subcode);

public sealed record WhatsAppErrorEnvelope([property: JsonPropertyName("error")] WhatsAppError? Error);

public sealed record WhatsAppCallResult(bool Ok, bool Retryable, string? Error, bool ConfigurationFault = false);

/// <summary>
/// One HTTP call against the Meta Cloud API. No SDK and no intermediary: sending a template message
/// is a single POST, and routing it through a provider would add an account model and a per-message
/// markup for no capability Everdue needs.
///
/// **Outbound only.** Without a public webhook there are no delivery or read callbacks, so a
/// successful call means "Meta accepted it", not "it reached a phone". Everything that reports on
/// this channel says so.
/// </summary>
public sealed class WhatsAppCloudApiClient(HttpClient http)
{
    private const string ApiVersion = "v21.0";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Meta's error codes, classified once. Template faults are configuration mistakes that will fail
    /// identically forever, so they are permanent *and* worth surfacing to an administrator rather
    /// than burying in a retry loop.
    /// </summary>
    private const int TemplateParamMismatch = 132000;
    private const int TemplateNotFound = 132001;
    private const int MessageUndeliverable = 131026;

    public async Task<WhatsAppCallResult> SendTemplateAsync(
        string phoneNumberId,
        string accessToken,
        string toPhoneE164,
        string templateName,
        string templateLanguage,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = toPhoneE164.TrimStart('+'),
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = templateLanguage },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = arguments.Select(value => new { type = "text", text = value }).ToArray(),
                    },
                },
            },
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiVersion}/{phoneNumberId}/messages")
            {
                Content = JsonContent.Create(payload, options: Json),
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await http.SendAsync(request, cancellationToken);
            return await ClassifyAsync(response, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return new WhatsAppCallResult(false, Retryable: true, $"WhatsApp unreachable: {e.Message}");
        }
    }

    private static async Task<WhatsAppCallResult> ClassifyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return new WhatsAppCallResult(true, false, null);
        }

        var error = await ReadErrorAsync(response, cancellationToken);
        var description = error?.Message ?? $"HTTP {(int)response.StatusCode}";
        var code = error?.Code;

        if (code is TemplateParamMismatch or TemplateNotFound)
        {
            // Every message of this type will fail the same way until somebody fixes the template.
            return new WhatsAppCallResult(false, Retryable: false, $"[{code}] {description}", ConfigurationFault: true);
        }

        if (code == MessageUndeliverable)
        {
            // Meta's deliberate bucket error: blocked business, not on WhatsApp, frequency capping.
            // The honest reading is "this number may not be reachable", not "try again in a minute".
            return new WhatsAppCallResult(false, Retryable: false, $"[{code}] {description}");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new WhatsAppCallResult(false, Retryable: false, description, ConfigurationFault: true);
        }

        var retryable = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
        return new WhatsAppCallResult(false, retryable, description);
    }

    private static async Task<WhatsAppError?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = await response.Content.ReadFromJsonAsync<WhatsAppErrorEnvelope>(Json, cancellationToken);
            return envelope?.Error;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
