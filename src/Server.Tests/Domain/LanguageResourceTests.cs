using Everdue.Server.Application.Localization;
using Everdue.Server.Domain;

namespace Everdue.Server.Tests.Domain;

/// <summary>
/// The test that catches the one step in adding a language that fails <em>silently</em>.
///
/// A culture missing from <c>SatelliteResourceLanguages</c> in <c>Everdue.Server.csproj</c> has its satellite
/// assembly dropped from the build output. Nothing errors: the screens are translated, and the digest quietly
/// arrives in English. Asserting that every supported language actually resolves a string turns that into a build
/// failure instead of a customer noticing months later.
/// </summary>
public class LanguageResourceTests
{
    /// <summary>Every family the server renders text from, with a key each that must exist in all of them.</summary>
    public static TheoryData<string, string> Sentinels()
    {
        var data = new TheoryData<string, string>();

        foreach (var language in Languages.Supported)
        {
            data.Add(language, NotificationText.SubjectPrefix + "." + nameof(NotificationType.Missed));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Sentinels))]
    public void Every_supported_language_resolves_a_notification_string(string language, string key)
    {
        var text = AppText.Notifications.Get(language, key);

        // ResourceTranslator returns the key itself when nothing resolves, which is exactly the failure a missing
        // satellite assembly produces.
        text.ShouldNotBe(key, $"'{key}' does not resolve in '{language}'. Check Resources/NotificationStrings.{language}.resx and SatelliteResourceLanguages.");
        text.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Every_supported_language_resolves_the_digest_and_bot_families()
    {
        foreach (var language in Languages.Supported)
        {
            var subject = AppText.Digest.Get(language, DigestText.Subject);
            subject.ShouldNotBe(DigestText.Subject, $"The digest does not resolve in '{language}'.");

            var confirmation = AppText.Bot.Get(language, BotText.Linked);
            confirmation.ShouldNotBe(BotText.Linked, $"The bot text does not resolve in '{language}'.");
        }
    }

    [Fact]
    public void Every_supported_language_has_a_culture_and_a_native_name()
    {
        foreach (var language in Languages.Supported)
        {
            Languages.IsSupported(language).ShouldBeTrue();
            Languages.Culture(language).ShouldNotBeNull();

            // What the picker shows. A language that calls itself by its code is a language nobody chose a name for.
            Languages.NativeName(language).ShouldNotBe(language);
            Languages.NativeName(language).ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Spanish_and_English_are_both_present()
    {
        // Not a tautology: they are the two the product promises, and a refactor that dropped one would otherwise
        // only be noticed by a screenshot.
        Languages.Supported.ShouldContain(Languages.Spanish);
        Languages.Supported.ShouldContain(Languages.English);
    }

    [Fact]
    public void An_unsupported_preference_falls_back_rather_than_becoming_one()
    {
        // Null and "the tenant default" are different states, and a rejected value must not collapse into a real one.
        Languages.NormalizeOptional("kl").ShouldBeNull();
        Languages.Resolve("kl", Languages.English).ShouldBe(Languages.English);
        Languages.Normalize("kl").ShouldBe(Languages.Spanish);
    }
}
