using System.Globalization;

namespace Everdue.Server.Domain;

/// <summary>
/// Schema-only multi-tenancy for v1: exactly one row, resolved from configuration.
/// The column set exists so the hosted version is a no-migration step later.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = string.Empty;

    /// <summary>IANA identifier (e.g. "America/Bogota"). All period/due math converts through this.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Local hour of day (0-23) the daily digest is sent.</summary>
    public int DigestHourLocal { get; set; } = 7;

    /// <summary>Local hour of day (0-23) the "due today" reminders go out. Later than the digest by default:
    /// managers read before the day starts, the people doing the work want it once they have.</summary>
    public int ReminderHourLocal { get; set; } = 8;

    /// <summary>
    /// May this tenant fall back to the system's channel credentials when it has none of its own?
    /// True for self-host (where "system" and "tenant" are the same operator anyway). The hosted
    /// product's free plan turns it off; the billing that would toggle it is v3 machinery.
    /// </summary>
    public bool CanUseSystemChannels { get; set; } = true;

    /// <summary>"es" or "en" — used when a user has no PreferredLanguage.</summary>
    public string DefaultLanguage { get; set; } = Languages.Spanish;

    /// <summary>
    /// This tenant holds demo data, not real work. Persisted rather than derived because there is no honest
    /// heuristic for it — seeded history is indistinguishable from real history by design, which is the whole
    /// point of it — and because every screen needs to be able to say so out loud.
    ///
    /// Set only by the demo-mode endpoint, which wipes the tenant on the way through in both directions.
    /// </summary>
    public bool DemoMode { get; set; }

    public bool Active { get; set; } = true;

    public TimeZoneInfo ResolveTimeZone() => TimeZoneLookup.Resolve(TimeZoneId);
}

public static class Languages
{
    public const string Spanish = "es";
    public const string English = "en";

    /// <summary>
    /// The languages this build can render, and the only list that decides.
    ///
    /// Adding one is four data files and two entries — <c>client/src/i18n/locales/{code}.json</c>, the three
    /// <c>Resources/*.{code}.resx</c> families, this array, and <c>SatelliteResourceLanguages</c> in
    /// <c>Everdue.Server.csproj</c>. The last one is the trap: a culture missing from it has its satellite
    /// assembly silently dropped from the build, and a "translated" digest then arrives in English. A test
    /// asserts every code here resolves in every resx family, so that failure is loud.
    ///
    /// See <c>docs/translating.md</c>.
    /// </summary>
    public static readonly string[] Supported = [Spanish, English];

    /// <summary>
    /// What a language calls itself. In its own language, because a picker that offers "Spanish" to somebody who
    /// only reads Spanish is a picker they cannot use.
    /// </summary>
    public static string NativeName(string? language) => Normalize(language) switch
    {
        English => "English",
        Spanish => "Español",
        var code => CultureInfo.GetCultureInfo(code).NativeName,
    };

    public static bool IsSupported(string? language)
        => language is not null && Supported.Contains(language, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? language)
        => IsSupported(language) ? language!.ToLowerInvariant() : Spanish;

    /// <summary>
    /// The culture that formats dates and numbers for a language. Neutral on purpose: the tenant's
    /// time zone decides the instant, the language only decides how it reads.
    /// </summary>
    public static CultureInfo Culture(string? language) => CultureInfo.GetCultureInfo(Normalize(language));

    /// <summary>
    /// The first of <paramref name="preferred"/> and <paramref name="fallback"/> that names a supported
    /// language — "the user's choice, else the tenant's default". Stated once here because five call
    /// sites had grown their own copy of it.
    /// </summary>
    public static string Resolve(string? preferred, string? fallback)
        => IsSupported(preferred) ? Normalize(preferred) : Normalize(fallback);

    /// <summary>
    /// The stored form of an *optional* preference: null when the value names no supported language.
    /// Null and "the tenant default" are different states — the first follows the tenant if an admin
    /// changes it, the second does not — so a rejected value must not collapse into a real one.
    /// </summary>
    public static string? NormalizeOptional(string? language)
        => IsSupported(language) ? Normalize(language) : null;
}
