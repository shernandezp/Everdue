using System.Net;
using System.Text.Json;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Localization;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Notifications;

/// <summary>
/// Renders a notification for a channel, in the recipient's stored language. The text itself lives in
/// <c>Resources/NotificationStrings*.resx</c>; this class only decides which keys to ask for.
///
/// The in-app bell does **not** come through here — it renders the same parameters client-side in the
/// reader's UI language. One set of facts, two renderers: the alternative is storing rendered text,
/// which freezes a message into whichever language the sender happened to be using.
/// </summary>
public static class NotificationTemplates
{
    /// <summary>Untranslated on purpose: the product's name reads the same in every language.</summary>
    private const string ProductName = "Everdue";

    /// <summary>The WhatsApp template the "send me a test" button maps to.</summary>
    private const string TestTemplateKey = "Test";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static ResourceTranslator Text => AppText.Notifications;

    /// <summary>
    /// Builds the message every channel variant needs at once: a line of text, an HTML body, and the
    /// positional variables a pre-approved WhatsApp template expects. The three template arguments
    /// are the same for every type — title, context, actor — so all five templates submitted to Meta
    /// have an identical shape.
    /// </summary>
    public static ChannelMessage Render(Notification notification, string language, string? publicBaseUrl)
    {
        var data = ReadData(notification.DataJson);
        var normalized = Languages.Normalize(language);

        var title = Value(data, NotificationData.Title) ?? Text[normalized, NotificationText.Empty];
        var actor = Value(data, NotificationData.Actor) ?? Text[normalized, NotificationText.Someone];
        var context = ContextLine(data, normalized);

        // The body's resource key is the NotificationType member name; the subject's is prefixed.
        var text = Text.Format(normalized, notification.Type.ToString(), title, context, actor);
        var link = LinkTo(notification, publicBaseUrl);

        return new ChannelMessage(
            Subject: Text.Enum(normalized, NotificationText.SubjectPrefix, notification.Type),
            PlainText: link is null ? text : $"{text}\n{link}",
            HtmlBody: Html(text, context, link, normalized),
            TemplateKey: notification.Type.ToString(),
            TemplateArgs: [title, string.IsNullOrWhiteSpace(context) ? Text[normalized, NotificationText.Empty] : context, actor],
            Language: normalized);
    }

    /// <summary>The settings screen's "send me a test" button. Deliberately the dullest message possible.</summary>
    public static ChannelMessage RenderTest(string language)
    {
        var normalized = Languages.Normalize(language);
        var text = Text[normalized, NotificationText.TestMessage];

        return new ChannelMessage(
            Subject: ProductName,
            PlainText: text,
            HtmlBody: $"<p>{WebUtility.HtmlEncode(text)}</p>",
            TemplateKey: TestTemplateKey,
            TemplateArgs: [text, Text[normalized, NotificationText.Empty], ProductName],
            Language: normalized);
    }

    private static string ContextLine(IReadOnlyDictionary<string, string?> data, string language)
    {
        if (Value(data, NotificationData.Entity) is { } entity)
        {
            return Text.Format(language, NotificationText.ContextEntity, entity);
        }

        if (Value(data, NotificationData.DueDate) is { } due)
        {
            return Text.Format(language, NotificationText.ContextDue, due);
        }

        return string.Empty;
    }

    private static string? LinkTo(Notification notification, string? publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(publicBaseUrl) || notification.WorkItemId is not { } workItemId)
        {
            return null;
        }

        return $"{publicBaseUrl.TrimEnd('/')}{ClientRoutes.WorkItem(workItemId)}";
    }

    private static string Html(string text, string context, string? link, string language)
    {
        var body = EmailHtml.Paragraph(text, EmailHtml.BodyStyle);

        if (!string.IsNullOrWhiteSpace(context))
        {
            body += EmailHtml.Paragraph(context, EmailHtml.MutedStyle);
        }

        if (link is not null)
        {
            body += $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(Text[language, NotificationText.OpenLink])}</a></p>";
        }

        return body;
    }

    private static IReadOnlyDictionary<string, string?> ReadData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>();
        }
    }

    private static string? Value(IReadOnlyDictionary<string, string?> data, string key)
        => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
