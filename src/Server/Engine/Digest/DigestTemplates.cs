using System.Text;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Localization;
using Everdue.Server.Domain;

namespace Everdue.Server.Engine.Digest;

/// <summary>
/// The digest is the one substantial piece of user-facing text the server renders, so it is the one
/// place the server needs translations. They live in <c>Resources/DigestStrings*.resx</c>; the markup
/// lives in <see cref="EmailHtml"/>. What is left here is which sections a digest has and in what
/// order — which is the only part worth reading this file for.
/// </summary>
public static class DigestTemplates
{
    private static ResourceTranslator Text => AppText.Digest;

    public static string Subject(DigestContent content, string language)
        => Text.Format(
            language,
            WithFrequency(DigestText.Subject, content.Frequency),
            FormatDate(content.LocalDate, language));

    public static string RenderHtml(DigestContent content, string language)
    {
        var html = new StringBuilder();

        html.OpenDocument()
            .Title(content.TenantName)
            .Subtitle(Text.Format(language, WithFrequency(DigestText.Greeting, content.Frequency), FormatDate(content.LocalDate, language)));

        if (content.DepartmentName is { } department)
        {
            html.Caption(Text.Format(language, DigestText.Department, department));
        }
        else
        {
            html.Spacer();
        }

        AppendItems(html, WithFrequency(DigestText.WentMissed, content.Frequency), content.WentMissed, content, language, EmailHtml.Accent.Alarm);
        AppendItems(html, DigestText.DueToday, content.DueToday, content, language, EmailHtml.Accent.Due);
        AppendHolds(html, content, language);
        AppendAging(html, content, language);
        AppendNeglect(html, content, language);

        html.Footer(Text[language, DigestText.Footer]).CloseDocument();

        return html.ToString();
    }

    /// <summary>Weekly wording where it differs, falling back to the daily string where it does not.</summary>
    private static string WithFrequency(string key, DigestFrequency frequency)
        => frequency == DigestFrequency.Weekly ? $"{key}.{DigestText.WeeklyVariant}" : key;

    private static void AppendItems(
        StringBuilder html,
        string headingKey,
        IReadOnlyList<DigestItem> items,
        DigestContent content,
        string language,
        string accent)
    {
        if (!OpenSection(html, Text[language, headingKey], items.Count, accent, language))
        {
            return;
        }

        html.OpenTable(
            Text[language, DigestText.Task],
            Text[language, DigestText.Entity],
            Text[language, DigestText.Owner],
            Text[language, DigestText.Due]);

        var culture = Languages.Culture(language);

        foreach (var item in items)
        {
            // Times are rendered in the tenant's zone; the language only controls the format.
            var due = TenantTime.LocalDateTime(item.DueDate, content.TimeZone).ToString("g", culture);

            html.Row(item.Title, item.EntityName ?? Text[language, DigestText.Empty], item.OwnerName, due);
        }

        html.CloseTable();
    }

    private static void AppendHolds(StringBuilder html, DigestContent content, string language)
    {
        var total = content.OnHold.Sum(group => group.Count);

        if (!OpenSection(html, Text[language, DigestText.OnHold], total, EmailHtml.Accent.Waiting, language, content.OnHold.Count))
        {
            return;
        }

        html.OpenTable(Text[language, DigestText.Reason], Text[language, DigestText.Count]);

        foreach (var group in content.OnHold.OrderByDescending(group => group.Count))
        {
            html.Row(ReasonName(group.Reason, language), Number(group.Count, language));
        }

        html.CloseTable();
    }

    /// <summary>
    /// Where the work has been stuck longest, and on whom. The dashboard shows the same grouping;
    /// this is the version a manager sees without opening anything.
    /// </summary>
    private static void AppendAging(StringBuilder html, DigestContent content, string language)
    {
        if (!OpenSection(html, Text[language, DigestText.Aging], content.OnHoldAging.Count, EmailHtml.Accent.Waiting, language))
        {
            return;
        }

        html.OpenTable(
            Text[language, DigestText.Entity],
            Text[language, DigestText.Reason],
            Text[language, DigestText.Count],
            Text[language, DigestText.Days]);

        foreach (var row in content.OnHoldAging)
        {
            html.Row(row.EntityName, ReasonName(row.Reason, language), Number(row.Count, language), Number(row.OldestDays, language));
        }

        html.CloseTable();
    }

    private static void AppendNeglect(StringBuilder html, DigestContent content, string language)
    {
        if (!OpenSection(html, Text[language, DigestText.Neglect], content.Neglect.Count, EmailHtml.Accent.Stale, language))
        {
            return;
        }

        html.OpenTable(
            Text[language, DigestText.Entity],
            Text[language, DigestText.Days],
            Text[language, DigestText.Open]);

        foreach (var row in content.Neglect)
        {
            var days = row.DaysSinceLastActivity is { } value
                ? Number(value, language)
                : Text[language, DigestText.Never];

            html.Row(row.EntityName, days, Number(row.OpenCount, language));
        }

        html.CloseTable();
    }

    /// <summary>
    /// Writes a section's heading and returns whether it has rows worth a table. The count in the
    /// heading is not always the row count — the hold section counts items across its groups — so it
    /// is passed separately from <paramref name="rowCount"/>.
    /// </summary>
    private static bool OpenSection(
        StringBuilder html,
        string heading,
        int headingCount,
        string accent,
        string language,
        int? rowCount = null)
    {
        html.SectionHeading(heading, headingCount, accent);

        if ((rowCount ?? headingCount) > 0)
        {
            return true;
        }

        html.EmptySection(Text[language, DigestText.Nothing]);
        return false;
    }

    public static string ReasonName(HoldReason reason, string language)
        => Text.Enum(language, DigestText.ReasonPrefix, reason);

    /// <summary>A section heading on its own, for the plain-text part of the mail.</summary>
    public static string Section(string language, string key) => Text[language, key];

    private static string Number(int value, string language) => value.ToString(Languages.Culture(language));

    private static string FormatDate(DateOnly date, string language)
        => date.ToDateTime(TimeOnly.MinValue).ToString("d", Languages.Culture(language));
}
