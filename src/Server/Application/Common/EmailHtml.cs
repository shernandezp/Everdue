using System.Net;
using System.Text;

namespace Everdue.Server.Application.Common;

/// <summary>
/// The small amount of HTML the server sends by e-mail.
///
/// Mail clients ignore stylesheets, so every rule has to be inline on the element — which is how five
/// digest sections ended up carrying their own copy of the same table markup. The styles are named
/// here once, and callers append semantic pieces instead of strings.
///
/// Everything that takes text encodes it. There is no overload that does not: a task title is
/// user-supplied, and it reaches an inbox.
/// </summary>
public static class EmailHtml
{
    public const string BodyStyle = "font-family:system-ui,sans-serif;font-size:15px";
    public const string MutedStyle = "font-family:system-ui,sans-serif;color:#6b7280;font-size:13px";

    private const string FontStack = "system-ui,-apple-system,Segoe UI,Roboto,sans-serif";
    private const string DocumentStyle = $"font-family:{FontStack};color:#1f2937;";
    private const string HeadingStyle = "margin:24px 0 8px";
    private const string SubduedStyle = "margin:0;color:#6b7280";
    private const string TableStyle = "border-collapse:collapse;width:100%;font-size:14px";
    private const string HeaderRowStyle = "text-align:left;background:#f3f4f6";
    private const string BodyRowStyle = "border-top:1px solid #e5e7eb";

    /// <summary>Section accents. Named by meaning so a section cannot pick a colour that says nothing.</summary>
    public static class Accent
    {
        public const string Alarm = "#b91c1c";
        public const string Due = "#1d4ed8";
        public const string Waiting = "#b45309";
        public const string Stale = "#6d28d9";
    }

    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public static string Paragraph(string text, string style)
        => $"<p style=\"{style}\">{Encode(text)}</p>";

    public static StringBuilder OpenDocument(this StringBuilder html)
        => html.Append($"<!doctype html><html><body style=\"{DocumentStyle}\">");

    public static StringBuilder CloseDocument(this StringBuilder html)
        => html.Append("</body></html>");

    public static StringBuilder Title(this StringBuilder html, string text)
        => html.Append($"<h2 style=\"margin:0 0 4px\">{Encode(text)}</h2>");

    public static StringBuilder Subtitle(this StringBuilder html, string text)
        => html.Append($"<p style=\"margin:0 0 4px;color:#6b7280\">{Encode(text)}</p>");

    public static StringBuilder Caption(this StringBuilder html, string text)
        => html.Append($"<p style=\"margin:0 0 20px;color:#6b7280\">{Encode(text)}</p>");

    public static StringBuilder Spacer(this StringBuilder html)
        => html.Append("<div style=\"height:16px\"></div>");

    public static StringBuilder Footer(this StringBuilder html, string text)
        => html.Append($"<p style=\"margin-top:28px;font-size:12px;color:#9ca3af\">{Encode(text)}</p>");

    /// <summary>A section heading carrying its own count, which is what makes a digest skimmable.</summary>
    public static StringBuilder SectionHeading(this StringBuilder html, string heading, int count, string accent)
        => html.Append($"<h3 style=\"{HeadingStyle};color:{accent}\">{Encode(heading)} ({count})</h3>");

    public static StringBuilder EmptySection(this StringBuilder html, string text)
        => html.Append($"<p style=\"{SubduedStyle}\">{Encode(text)}</p>");

    public static StringBuilder OpenTable(this StringBuilder html, params string[] headers)
    {
        html.Append($"<table cellpadding=\"6\" cellspacing=\"0\" style=\"{TableStyle}\">");
        html.Append($"<tr style=\"{HeaderRowStyle}\">");

        foreach (var header in headers)
        {
            html.Append($"<th>{Encode(header)}</th>");
        }

        return html.Append("</tr>");
    }

    /// <summary>
    /// One body row. Cells arrive as already-rendered display strings — a caller that wants a number
    /// formatted for the reader's language formats it before handing it over, so this method never has
    /// to guess at a culture.
    /// </summary>
    public static StringBuilder Row(this StringBuilder html, params string?[] cells)
    {
        html.Append($"<tr style=\"{BodyRowStyle}\">");

        foreach (var cell in cells)
        {
            html.Append($"<td>{Encode(cell)}</td>");
        }

        return html.Append("</tr>");
    }

    public static StringBuilder CloseTable(this StringBuilder html) => html.Append("</table>");
}
