using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Everdue.Server.Infrastructure.Channels;

/// <summary>What the Bot API returns for every method: a success flag and, on failure, a reason.</summary>
public sealed record TelegramResponse<T>(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] T? Result,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("error_code")] int? ErrorCode,
    [property: JsonPropertyName("parameters")] TelegramResponseParameters? Parameters);

public sealed record TelegramResponseParameters(
    [property: JsonPropertyName("retry_after")] int? RetryAfter);

public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message);

public sealed record TelegramMessage(
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("text")] string? Text);

public sealed record TelegramChat([property: JsonPropertyName("id")] long Id);

/// <summary>The outcome of a Bot API call, already classified into what the outbox needs to know.</summary>
public sealed record TelegramCallResult(bool Ok, bool Retryable, string? Error, TimeSpan? RetryAfter = null, bool ChatUnreachable = false);

/// <summary>One poll: whether it reached Telegram at all, and what came back.</summary>
public sealed record TelegramUpdates(bool Ok, IReadOnlyList<TelegramUpdate> Updates)
{
    public static readonly TelegramUpdates Failed = new(false, []);
}

/// <summary>
/// A thin wrapper over the two Bot API methods Everdue uses. Deliberately not a library: two
/// endpoints, no state, and one fewer dependency in a single-file publish.
/// </summary>
public sealed class TelegramApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<TelegramCallResult> SendMessageAsync(
        string botToken,
        long chatId,
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(
                $"bot{botToken}/sendMessage",
                new { chat_id = chatId, text, disable_web_page_preview = true },
                Json,
                cancellationToken);

            return await ClassifyAsync(response, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return new TelegramCallResult(false, Retryable: true, $"Telegram unreachable: {e.Message}");
        }
    }

    /// <summary>
    /// Long polling. No webhook, and therefore no public HTTPS endpoint — which is the difference
    /// between a channel a self-hoster behind a router can use and one they cannot.
    ///
    /// <see cref="TelegramUpdates.Ok"/> is not decoration: a successful long poll blocks for the
    /// timeout before returning nothing, while a failed call returns immediately. A caller that
    /// could not tell them apart would spin on a network outage.
    /// </summary>
    public async Task<TelegramUpdates> GetUpdatesAsync(
        string botToken,
        long offset,
        int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(
                $"bot{botToken}/getUpdates",
                new { offset, timeout = timeoutSeconds, allowed_updates = new[] { "message" } },
                Json,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return TelegramUpdates.Failed;
            }

            var payload = await response.Content.ReadFromJsonAsync<TelegramResponse<TelegramUpdate[]>>(Json, cancellationToken);
            return new TelegramUpdates(true, payload?.Result ?? []);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            // A dropped connection or a restart mid-long-poll is ordinary; the next pass asks again
            // from the same offset, after the caller has waited.
            return TelegramUpdates.Failed;
        }
    }

    private static async Task<TelegramCallResult> ClassifyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await ReadAsync(response, cancellationToken);

        if (response.IsSuccessStatusCode && payload?.Ok == true)
        {
            return new TelegramCallResult(true, false, null);
        }

        var description = payload?.Description ?? $"HTTP {(int)response.StatusCode}";

        // 429 comes with the exact number of seconds to wait; honouring it is the difference between
        // backing off and being rate-limited harder.
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = payload?.Parameters?.RetryAfter is { } seconds
                ? TimeSpan.FromSeconds(seconds)
                : (TimeSpan?)null;

            return new TelegramCallResult(false, Retryable: true, description, retryAfter);
        }

        // 401/404 = the token is wrong or gone. 403 = this user blocked the bot, which no amount of
        // retrying fixes; the caller clears their chat id so they stop generating dead deliveries.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
        {
            return new TelegramCallResult(false, Retryable: false, description);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden || (payload?.ErrorCode == 400 && IsChatGone(description)))
        {
            return new TelegramCallResult(false, Retryable: false, description, ChatUnreachable: true);
        }

        return new TelegramCallResult(false, Retryable: (int)response.StatusCode >= 500, description);
    }

    private static bool IsChatGone(string description)
        => description.Contains("chat not found", StringComparison.OrdinalIgnoreCase)
           || description.Contains("user is deactivated", StringComparison.OrdinalIgnoreCase);

    private static async Task<TelegramResponse<object>?> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<TelegramResponse<object>>(Json, cancellationToken);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
