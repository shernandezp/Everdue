using System.Text.Json;
using System.Text.Json.Nodes;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Channels;

/// <summary>
/// What an administrator may see and edit of a channel's configuration.
///
/// The rule is narrow and absolute: **every secret is removed, everything else round-trips.** A form
/// that could not show the stored host, port, bot username or template names would force somebody to
/// retype all of them to change one — and re-typing a token they cannot read is the one thing this
/// deliberately does not ask of them (that is what the blank-means-keep rule is for).
/// </summary>
internal static class ChannelConfigView
{
    /// <summary>Property names stripped on the way out, matched case-insensitively.</summary>
    private static readonly string[] Secrets = ["password", "botToken", "accessToken"];

    public static string? Redact(string configJson)
    {
        try
        {
            if (JsonNode.Parse(configJson) is not JsonObject root)
            {
                return null;
            }

            foreach (var name in root.Select(pair => pair.Key)
                         .Where(key => Secrets.Contains(key, StringComparer.OrdinalIgnoreCase))
                         .ToArray())
            {
                root[name] = string.Empty;
            }

            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// One line naming the account a channel points at — host, bot, phone-number id. Not a
    /// credential, and it is what an administrator actually checks: "is this the right thing".
    /// </summary>
    public static string? Describe(NotificationChannel channel, string configJson) => channel switch
    {
        NotificationChannel.Email => ChannelConfigJson.Deserialize<SmtpChannelConfig>(configJson) is { } smtp
            ? $"{smtp.Host}:{smtp.Port} · {smtp.From}"
            : null,

        NotificationChannel.Telegram => ChannelConfigJson.Deserialize<TelegramChannelConfig>(configJson) is { } telegram
            ? $"@{telegram.BotUsername?.TrimStart('@') ?? "bot"}"
            : null,

        NotificationChannel.WhatsApp => ChannelConfigJson.Deserialize<WhatsAppChannelConfig>(configJson) is { } whatsApp
            ? $"{whatsApp.PhoneNumberId} · {whatsApp.Templates.Count} template(s) · {whatsApp.TemplateLanguage}"
            : null,

        _ => null,
    };

    /// <summary>
    /// Refuses a configuration that could never send. Without this, saving a typo leaves the screen
    /// saying "not configured" with no explanation of what is missing.
    /// </summary>
    public static void EnsureUsable(NotificationChannel channel, string configJson)
    {
        var problem = channel switch
        {
            NotificationChannel.Email => ChannelConfigJson.Deserialize<SmtpChannelConfig>(configJson) is { IsUsable: true }
                ? null
                : "A host and a from-address are required.",

            NotificationChannel.Telegram => ChannelConfigJson.Deserialize<TelegramChannelConfig>(configJson) is { IsUsable: true }
                ? null
                : "A bot token is required.",

            NotificationChannel.WhatsApp => ChannelConfigJson.Deserialize<WhatsAppChannelConfig>(configJson) is { IsUsable: true }
                ? null
                : "A phone-number id and an access token are required.",

            _ => null,
        };

        if (problem is not null)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["configJson"] = [problem] });
        }
    }
}
