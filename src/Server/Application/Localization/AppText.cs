using System.Resources;

namespace Everdue.Server.Application.Localization;

/// <summary>
/// The server's translated text, one <see cref="ResourceTranslator"/> per <c>.resx</c> family in
/// <c>Resources/</c>.
///
/// Only text the *server* renders lives here: notification bodies, digest e-mails, and the two
/// sentences the Telegram bot says. Everything a user reads inside the app is translated in the SPA,
/// and API error messages stay English on purpose — they are developer-facing.
/// </summary>
public static class AppText
{
    private const string Prefix = "Everdue.Server.Resources.";

    public static readonly ResourceTranslator Notifications = For("NotificationStrings");

    public static readonly ResourceTranslator Digest = For("DigestStrings");

    public static readonly ResourceTranslator Bot = For("BotStrings");

    private static ResourceTranslator For(string family)
        => new(new ResourceManager(Prefix + family, typeof(AppText).Assembly));
}

/// <summary>
/// Keys into <see cref="AppText.Notifications"/>. Named rather than typed at the call site so a
/// renamed resource breaks the build instead of rendering the key to a user.
/// </summary>
public static class NotificationText
{
    /// <summary>Prefix for the per-<c>NotificationType</c> body; the type name completes it.</summary>
    public const string SubjectPrefix = "subject";

    public const string ContextEntity = "context.entity";
    public const string ContextDue = "context.due";
    public const string Someone = "someone";
    public const string Empty = "none";
    public const string OpenLink = "open";
    public const string TestMessage = "test";
}

/// <summary>Keys into <see cref="AppText.Digest"/>.</summary>
public static class DigestText
{
    /// <summary>Variant suffix for the weekly wording (see <see cref="ResourceTranslator.Variant"/>).</summary>
    public const string WeeklyVariant = "weekly";

    public const string Subject = "subject";
    public const string Greeting = "greeting";
    public const string Department = "department";
    public const string WentMissed = "wentMissed";
    public const string DueToday = "dueToday";
    public const string OnHold = "onHold";
    public const string Aging = "aging";
    public const string Neglect = "neglect";
    public const string Days = "days";
    public const string Never = "never";
    public const string Open = "open";
    public const string Nothing = "nothing";
    public const string Owner = "owner";
    public const string Entity = "entity";
    public const string Due = "due";
    public const string Task = "task";
    public const string Count = "count";
    public const string Reason = "reason";
    public const string Footer = "footer";

    /// <summary>Placeholder for a cell with nothing in it — an unlinked entity, a missing owner.</summary>
    public const string Empty = "empty";

    /// <summary>Prefix for <see cref="Domain.HoldReason"/> display names.</summary>
    public const string ReasonPrefix = "reason";
}

/// <summary>Keys into <see cref="AppText.Bot"/>.</summary>
public static class BotText
{
    public const string Linked = "linked";
    public const string LinkFailed = "linkFailed";
}
