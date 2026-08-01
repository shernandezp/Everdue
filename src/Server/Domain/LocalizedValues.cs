using System.Globalization;

namespace Everdue.Server.Domain;

/// <summary>
/// Reading a number or a date that a person typed, in a product shipped in Spanish and English.
///
/// One rule, in one place, because there are two front doors — the entity form and the CSV import — and a value
/// that can be typed in must also be importable, and vice versa.
/// </summary>
public static class LocalizedValues
{
    /// <summary>
    /// <strong>Thousands separators are deliberately not accepted.</strong> They are what makes the input ambiguous:
    /// with them allowed, <c>1200,5</c> parses under the invariant culture as <em>twelve thousand and five</em>
    /// rather than being rejected and retried as Spanish. Refusing <c>1.200,50</c> outright, with a message, is far
    /// better than silently storing a number a thousand times too large.
    /// </summary>
    private const NumberStyles NumberStyle =
        NumberStyles.AllowLeadingSign
        | NumberStyles.AllowDecimalPoint
        | NumberStyles.AllowLeadingWhite
        | NumberStyles.AllowTrailingWhite;

    /// <summary>Invariant first (so <c>1200.5</c> wins), then Spanish (so <c>1200,5</c> does).</summary>
    public static bool TryParseDecimal(string? value, out decimal parsed)
    {
        parsed = 0m;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return decimal.TryParse(value, NumberStyle, CultureInfo.InvariantCulture, out parsed)
               || decimal.TryParse(value, NumberStyle, CultureInfo.GetCultureInfo(Languages.Spanish), out parsed);
    }

    /// <summary>
    /// ISO first, then each shipped language's own order — a file exported from somebody's Excel carries whichever
    /// their machine chose, so <c>15/03/2026</c> and <c>3/15/2026</c> both have to work.
    ///
    /// A genuinely ambiguous date like <c>03/04/2026</c> is read as the *Spanish* order (4 March), because Spanish is
    /// the product's default language. That cannot be resolved from the value alone; ISO is the unambiguous form and
    /// the import hints say so.
    /// </summary>
    public static bool TryParseDate(string? value, out DateOnly parsed)
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return true;
        }

        foreach (var language in Languages.Supported)
        {
            if (DateOnly.TryParse(trimmed, Languages.Culture(language), DateTimeStyles.None, out parsed))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The canonical stored forms: culture-independent, so a value reads identically in every language.</summary>
    public static string Canonical(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Canonical(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
