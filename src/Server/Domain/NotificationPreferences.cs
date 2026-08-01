using System.Text.Json;
using System.Text.Json.Serialization;

namespace Everdue.Server.Domain;

/// <summary>
/// One JSON column on the user row, not a settings subsystem — there are five switches and one
/// choice, and a table would cost more than it explains.
///
/// Defaults are the product decision made visible: **every type on for in-app, no external channel**.
/// Nobody starts receiving messages they did not ask for (the owner's "per-event staff e-mails
/// default off"), while the app itself stops being silent from day one.
/// </summary>
public sealed class NotificationPreferences
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The one channel this person wants messages on. Null = in-app only.</summary>
    public NotificationChannel? Channel { get; set; }

    /// <summary>
    /// Keyed by <see cref="NotificationType"/> name. A missing key means "on", so a type added in a
    /// later version is enabled by default and an unknown key from a newer build is ignored rather
    /// than throwing.
    /// </summary>
    public Dictionary<string, bool> Types { get; set; } = [];

    public bool IsEnabled(NotificationType type)
        => !Types.TryGetValue(type.ToString(), out var enabled) || enabled;

    public void SetEnabled(NotificationType type, bool enabled) => Types[type.ToString()] = enabled;

    public static NotificationPreferences Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new NotificationPreferences();
        }

        try
        {
            return JsonSerializer.Deserialize<NotificationPreferences>(json, Json) ?? new NotificationPreferences();
        }
        catch (JsonException)
        {
            // A preference column is not worth failing a request over: fall back to the defaults.
            return new NotificationPreferences();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);
}
