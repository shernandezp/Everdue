using System.Resources;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Localization;

/// <summary>
/// Reads one family of <c>.resx</c> resources in a given language.
///
/// The point of going through <see cref="ResourceManager"/> rather than a dictionary in code is that
/// adding a language becomes a file: drop <c>&lt;Name&gt;.&lt;code&gt;.resx</c> beside the neutral one,
/// add the code to <see cref="Languages.Supported"/>, and every string the server renders follows.
/// Nothing in this class enumerates languages.
///
/// The neutral resource set is English, so a translation missing from a language's resx degrades to
/// English rather than to a resource key. A missing *key* returns the key itself — a visible defect
/// that no reader mistakes for content, and that a test can assert on.
/// </summary>
public sealed class ResourceTranslator(ResourceManager resources)
{
    public string this[string language, string key] => Get(language, key);

    public string Get(string language, string key)
        => resources.GetString(key, Languages.Culture(language)) ?? key;

    /// <summary>
    /// A string with its placeholders filled, formatted for the reader's language. Numbers and dates
    /// passed as arguments are rendered by that language's rules, which is why the culture travels
    /// with the lookup instead of being read off the thread.
    /// </summary>
    public string Format(string language, string key, params object?[] arguments)
        => string.Format(Languages.Culture(language), Get(language, key), arguments);

    /// <summary>
    /// A string whose wording differs by variant, falling back to the base key where it does not —
    /// <c>"subject"</c> plus <c>"subject.weekly"</c>, so only the keys that actually change need a
    /// weekly form in every resx.
    /// </summary>
    public string Variant(string language, string key, string? variant)
    {
        if (string.IsNullOrEmpty(variant))
        {
            return Get(language, key);
        }

        return resources.GetString($"{key}.{variant}", Languages.Culture(language)) ?? Get(language, key);
    }

    /// <summary>An enum member's display name, by convention <c>&lt;prefix&gt;.&lt;member&gt;</c>.</summary>
    public string Enum<TEnum>(string language, string prefix, TEnum value)
        where TEnum : struct, System.Enum
        => Get(language, $"{prefix}.{value}");
}
