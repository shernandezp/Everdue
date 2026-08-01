using System.Globalization;
using System.Text;
using Everdue.Server.Application.Common;

namespace Everdue.Server.Application.Imports;

/// <summary>
/// Field keys, so the mapping travels as data rather than as reflection over a DTO.
/// </summary>
public static class ImportFields
{
    public const string Name = "name";
    public const string Type = "type";
    public const string Active = "active";

    public const string Title = "title";
    public const string Description = "description";
    public const string DueDate = "dueDate";
    public const string Owner = "owner";
    public const string Entity = "entity";
    public const string Department = "department";

    /// <summary>A custom field target is <c>custom:{definitionId}</c>, so the mapping stays a flat string map.</summary>
    public const string CustomPrefix = "custom:";

    public static string Custom(Guid definitionId) => CustomPrefix + definitionId;

    public static Guid? CustomDefinitionId(string key)
        => key.StartsWith(CustomPrefix, StringComparison.Ordinal) && Guid.TryParse(key[CustomPrefix.Length..], out var id)
            ? id
            : null;
}

/// <summary>
/// A confirmed column mapping, plus the header-matching that produces the suggestion.
///
/// Matching is accent- and case-insensitive and tries the field key, the English label and the Spanish label:
/// the file being imported was written by somebody's colleague in whichever language they work in, and
/// getting the first guess right is most of what makes the wizard feel like it works.
/// </summary>
public sealed class ImportMapping
{
    private readonly Dictionary<string, int> _columns;

    private ImportMapping(Dictionary<string, int> columns) => _columns = columns;

    /// <summary>Resolves the submitted field→header map against the real header row.</summary>
    public static ImportMapping Resolve(
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, string> mapping,
        IReadOnlyList<ImportFieldDto> fields)
    {
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        var errors = new Dictionary<string, string[]>();

        foreach (var (fieldKey, headerName) in mapping)
        {
            if (string.IsNullOrWhiteSpace(headerName))
            {
                continue;
            }

            var index = headers.ToList().FindIndex(h => string.Equals(h, headerName, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                errors[fieldKey] = [$"The file has no column named '{headerName}'."];
                continue;
            }

            columns[fieldKey] = index;
        }

        foreach (var field in fields.Where(f => f.Required))
        {
            if (!columns.ContainsKey(field.Key))
            {
                errors[field.Key] = [$"'{field.Label}' is required and has no column mapped to it."];
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return new ImportMapping(columns);
    }

    public string? Value(string[] row, string fieldKey)
    {
        if (!_columns.TryGetValue(fieldKey, out var index) || index >= row.Length)
        {
            return null;
        }

        var value = row[index].Trim();
        return value.Length == 0 ? null : value;
    }

    public IReadOnlyCollection<string> MappedFields => _columns.Keys;

    /// <summary>
    /// The first guess. A header matches a field when its normalised form equals the key or either label —
    /// nothing cleverer, because a wrong-but-confident guess is worse than an empty select.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Suggest(
        IReadOnlyList<string> headers,
        IReadOnlyList<ImportFieldDto> fields,
        IReadOnlyDictionary<string, string[]> aliases)
    {
        var suggestion = new Dictionary<string, string>(StringComparer.Ordinal);
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            var candidates = new List<string> { field.Key, field.Label };

            if (aliases.TryGetValue(field.Key, out var extra))
            {
                candidates.AddRange(extra);
            }

            var normalizedCandidates = candidates.Select(Normalize).ToHashSet(StringComparer.Ordinal);

            var match = headers.FirstOrDefault(h =>
                !taken.Contains(h) && normalizedCandidates.Contains(Normalize(h)));

            if (match is not null)
            {
                suggestion[field.Key] = match;
                taken.Add(match);
            }
        }

        return suggestion;
    }

    /// <summary>Lowercased, trimmed, stripped of accents and of anything that is not a letter or a digit.</summary>
    private static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
