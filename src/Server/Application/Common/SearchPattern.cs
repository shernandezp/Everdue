namespace Everdue.Server.Application.Common;

/// <summary>
/// Search has to mean the same thing on both providers. SQLite's <c>LIKE</c> is case-insensitive for
/// ASCII, PostgreSQL's is not — so "acme" would find "Acme Ltd" on a self-hosted install and find
/// nothing after a move to PostgreSQL. Both sides are lower-cased instead, which translates to
/// <c>lower()</c> everywhere and keeps the behaviour identical.
///
/// Wildcards in the user's own text are escaped, so searching for "50%" looks for "50%".
/// </summary>
public static class SearchPattern
{
    /// <summary>Passed to <c>EF.Functions.Like(…, escapeCharacter)</c> so it becomes a real SQL <c>ESCAPE</c> clause.</summary>
    public const string Escape = "\\";

    /// <summary>Returns null when there is nothing to search for, so callers can skip the filter entirely.</summary>
    public static string? For(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var escaped = search
            .Trim()
            .ToLowerInvariant()
            .Replace(Escape, Escape + Escape, StringComparison.Ordinal)
            .Replace("%", Escape + "%", StringComparison.Ordinal)
            .Replace("_", Escape + "_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
