using System.Globalization;
using System.Text;

namespace Everdue.Server.Domain;

public static class SearchText
{
    // Folding collapses every newline, so this can only ever appear between title and description.
    public const char TitleEnd = '\n';

    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var folded = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var rune in text.ToLowerInvariant().EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune) || Rune.IsControl(rune))
            {
                pendingSpace = folded.Length > 0;
                continue;
            }

            var bare = WithoutMarks(rune);
            if (bare.Length == 0)
            {
                continue;
            }

            if (pendingSpace)
            {
                folded.Append(' ');
                pendingSpace = false;
            }

            folded.Append(bare);
        }

        return folded.ToString();
    }

    // Rune by rune, and only where a mark comes off: decomposing whole strings triples the length
    // of Hangul, and the stored column is sized for the original text.
    private static string WithoutMarks(Rune rune)
    {
        var decomposed = rune.ToString().Normalize(NormalizationForm.FormD);
        var bare = string.Concat(decomposed.EnumerateRunes()
            .Where(r => Rune.GetUnicodeCategory(r) != UnicodeCategory.NonSpacingMark)
            .Select(r => r.ToString()));

        return bare.Length == decomposed.Length ? rune.ToString() : bare;
    }

    public static string Document(string title, string? description)
        => Fold(title) + TitleEnd + Fold(description);

    public static string[] Terms(string? query)
        => Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().Take(8).ToArray();
}
