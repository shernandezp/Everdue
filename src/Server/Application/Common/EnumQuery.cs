namespace Everdue.Server.Application.Common;

/// <summary>
/// Enum-valued query parameters are bound as strings and parsed here, case-insensitively.
///
/// Minimal APIs bind an enum with a case-<em>sensitive</em> <c>Enum.TryParse</c>, so
/// <c>?entityType=customer</c> would fail the model binder and produce a bare 400 with no body —
/// from a hand-typed URL, a bookmark, or anything that lower-cases links. Parsing here means the
/// obvious spelling works and a genuine typo gets an error that names the valid values.
/// </summary>
public static class EnumQuery
{
    public static TEnum? Parse<TEnum>(string? value, string parameterName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ValidationException(new Dictionary<string, string[]>
        {
            [parameterName] = [$"'{value}' is not a valid {typeof(TEnum).Name}. Expected one of: {string.Join(", ", Enum.GetNames<TEnum>())}."],
        });
    }

    public static TEnum ParseOr<TEnum>(string? value, string parameterName, TEnum fallback)
        where TEnum : struct, Enum
        => Parse<TEnum>(value, parameterName) ?? fallback;

    /// <summary>Comma-separated list, for filters that accept several values at once (status).</summary>
    public static TEnum[] ParseMany<TEnum>(string? value, string parameterName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Parse<TEnum>(part, parameterName)!.Value)
            .Distinct()
            .ToArray();
    }
}
