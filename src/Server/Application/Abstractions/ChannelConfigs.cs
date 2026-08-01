using System.Text.Json;
using System.Text.Json.Serialization;

namespace Everdue.Server.Application.Abstractions;

/// <summary>
/// Each channel owns the shape of its own configuration; the store only ever sees an opaque string.
/// That is why adding a future channel (SMS, Slack) touches no table and no resolver.
/// </summary>
public static class ChannelConfigJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}

public sealed class SmtpChannelConfig
{
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public string? User { get; set; }

    public string? Password { get; set; }

    public string? From { get; set; }

    public string? FromName { get; set; } = "Everdue";

    public bool UseStartTls { get; set; } = true;

    public bool IsUsable => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

public sealed class TelegramChannelConfig
{
    public string? BotToken { get; set; }

    /// <summary>Without the @. Only used to build the link the user taps; sending does not need it.</summary>
    public string? BotUsername { get; set; }

    public bool IsUsable => !string.IsNullOrWhiteSpace(BotToken);
}

/// <summary>
/// WhatsApp can only send pre-approved templates to somebody who has not messaged first, so the
/// configuration is mostly a map from "the thing that happened" to "the template Meta approved".
/// Template names are configuration rather than code precisely so an approval can land without a
/// deploy, and a type with no name simply falls through to the user's other channel.
/// </summary>
public sealed class WhatsAppChannelConfig
{
    public string? PhoneNumberId { get; set; }

    public string? AccessToken { get; set; }

    /// <summary>Meta's language code for the approved templates, e.g. "es" or "en_US".</summary>
    public string TemplateLanguage { get; set; } = "es";

    /// <summary>Keyed by <c>NotificationType</c> name, plus <c>Test</c> for the settings screen's test button.</summary>
    public Dictionary<string, string> Templates { get; set; } = [];

    public string? TemplateFor(string key)
        => Templates.TryGetValue(key, out var name) && !string.IsNullOrWhiteSpace(name) ? name : null;

    public bool IsUsable => !string.IsNullOrWhiteSpace(PhoneNumberId) && !string.IsNullOrWhiteSpace(AccessToken);
}
