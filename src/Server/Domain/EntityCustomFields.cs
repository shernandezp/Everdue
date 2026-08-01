using System.Globalization;
using System.Text.Json;

namespace Everdue.Server.Domain;

/// <summary>The outcome of checking one submitted value against its definition.</summary>
public sealed record CustomFieldValidation(bool Ok, string? Normalized, string? Error)
{
    public static CustomFieldValidation Valid(string? normalized) => new(true, normalized, null);

    public static CustomFieldValidation Invalid(string error) => new(false, null, error);
}

/// <summary>
/// Everything the <c>Entities.CustomFieldsJson</c> column knows how to do: parse, validate one value
/// against its definition, and serialise back. Pure — no EF, no DI, no I/O — so the rules are unit
/// testable and there is exactly one of them.
/// </summary>
public static class EntityCustomFields
{
    /// <summary>Longest a text value may be. Long enough for a name or a reference, short enough not to be a document.</summary>
    public const int MaxTextLength = 200;

    /// <summary>Most options a Select field may offer. A longer list is a lookup table, which entities are not.</summary>
    public const int MaxSelectOptions = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Values keyed by definition id. A malformed column reads as empty rather than throwing: a
    /// display-only field must never be able to make an entity unreadable.
    /// </summary>
    public static IReadOnlyDictionary<Guid, string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<Guid, string>();
        }

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            if (raw is null)
            {
                return new Dictionary<Guid, string>();
            }

            var parsed = new Dictionary<Guid, string>();
            foreach (var (key, value) in raw)
            {
                if (Guid.TryParse(key, out var id) && !string.IsNullOrWhiteSpace(value))
                {
                    parsed[id] = value;
                }
            }

            return parsed;
        }
        catch (JsonException)
        {
            return new Dictionary<Guid, string>();
        }
    }

    /// <summary>Null for an empty set, so an entity with no custom values stores no column content at all.</summary>
    public static string? Serialize(IReadOnlyDictionary<Guid, string> values)
        => values.Count == 0
            ? null
            : JsonSerializer.Serialize(
                values.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                JsonOptions);

    public static IReadOnlyList<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(optionsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string SerializeOptions(IEnumerable<string> options)
        => JsonSerializer.Serialize(options.ToArray(), JsonOptions);

    /// <summary>
    /// Checks and normalises one submitted value. Numbers and dates are stored in a canonical,
    /// culture-independent form so the same value reads identically in every language — the column is
    /// display-only, but "display-only" is not an excuse for storing whatever arrived.
    /// </summary>
    public static CustomFieldValidation Validate(EntityFieldDef definition, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            // Clearing a field is always allowed: nothing here is ever required (a required-field
            // workflow would make a custom field drive behaviour, which is rejected by design).
            return CustomFieldValidation.Valid(null);
        }

        var trimmed = value.Trim();

        switch (definition.FieldType)
        {
            case EntityFieldType.Text:
                return trimmed.Length > MaxTextLength
                    ? CustomFieldValidation.Invalid($"Must be {MaxTextLength} characters or fewer.")
                    : CustomFieldValidation.Valid(trimmed);

            // Both go through LocalizedValues, so a value that can be typed into the form can also be imported —
            // and, more importantly, "1200,5" is never silently read as twelve thousand and five.
            case EntityFieldType.Number:
                return LocalizedValues.TryParseDecimal(trimmed, out var number)
                    ? CustomFieldValidation.Valid(LocalizedValues.Canonical(number))
                    : CustomFieldValidation.Invalid("Must be a number, without thousands separators.");

            case EntityFieldType.Date:
                return LocalizedValues.TryParseDate(trimmed, out var date)
                    ? CustomFieldValidation.Valid(LocalizedValues.Canonical(date))
                    : CustomFieldValidation.Invalid("Must be a date, ideally as yyyy-MM-dd.");

            case EntityFieldType.Select:
                var options = ParseOptions(definition.OptionsJson);
                var match = options.FirstOrDefault(o => string.Equals(o, trimmed, StringComparison.OrdinalIgnoreCase));

                return match is null
                    ? CustomFieldValidation.Invalid($"Must be one of: {string.Join(", ", options)}.")
                    : CustomFieldValidation.Valid(match);

            default:
                return CustomFieldValidation.Invalid("Unknown field type.");
        }
    }
}
