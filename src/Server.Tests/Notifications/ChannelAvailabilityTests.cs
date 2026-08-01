using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Notifications;

/// <summary>
/// "Is this channel configured" has to have exactly one answer. The administrator's screen, the
/// health table and the list a user picks from all ask it, and an install that has been sending
/// mail since v1 — from appsettings, with no ChannelSettings row — must not be told otherwise.
/// </summary>
public class ChannelAvailabilityTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static Dictionary<string, string> WithSmtp => new()
    {
        ["Smtp:Host"] = "smtp.example.test",
        ["Smtp:From"] = "everdue@example.test",
    };

    /// <summary>The v1 upgrade path: SMTP in appsettings, no row anywhere, and e-mail is available.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Appsettings_smtp_counts_as_a_configured_email_channel(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, WithSmtp);
        var admin = await app.SignInAsAdminAsync();

        var listed = await admin.GetJsonAsync<IReadOnlyList<ChannelSettingsDto>>("/api/v1/settings/channels");
        listed.Single(c => c.Channel == NotificationChannel.Email).Configured.ShouldBeTrue();

        var health = await admin.GetJsonAsync<IReadOnlyList<ChannelHealthDto>>("/api/v1/settings/channels/health");
        health.Single(h => h.Channel == NotificationChannel.Email).Configured.ShouldBeTrue();

        var preferences = await admin.GetJsonAsync<NotificationPreferencesDto>("/api/v1/me/notification-preferences");
        preferences.AvailableChannels.ShouldContain(NotificationChannel.Email);
    }

    /// <summary>
    /// And the other direction, which is the one that actually bites: with no SMTP anywhere, e-mail
    /// must not be offered. Choosing it would mean every message silently becomes in-app only.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task With_no_smtp_at_all_email_is_not_offered(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var preferences = await admin.GetJsonAsync<NotificationPreferencesDto>("/api/v1/me/notification-preferences");

        preferences.AvailableChannels.ShouldBeEmpty();

        var listed = await admin.GetJsonAsync<IReadOnlyList<ChannelSettingsDto>>("/api/v1/settings/channels");
        listed.ShouldAllBe(c => !c.Configured);
    }

    /// <summary>
    /// The appsettings block is the operator's mail server under another name, so the flag that
    /// governs the system's credentials governs it too. On a hosted free plan, "bring your own"
    /// has to mean it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Appsettings_smtp_obeys_the_system_channel_flag(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider, WithSmtp);
        var admin = await app.SignInAsAdminAsync();

        var settings = await admin.GetJsonAsync<TenantSettingsDto>("/api/v1/settings/tenant");

        await admin.PutJsonAsync<TenantSettingsDto>("/api/v1/settings/tenant", new
        {
            name = settings.Name,
            timeZoneId = settings.TimeZoneId,
            digestHourLocal = settings.DigestHourLocal,
            defaultLanguage = settings.DefaultLanguage,
            reminderHourLocal = settings.ReminderHourLocal,
            canUseSystemChannels = false,
        });

        var preferences = await admin.GetJsonAsync<NotificationPreferencesDto>("/api/v1/me/notification-preferences");
        preferences.AvailableChannels.ShouldBeEmpty();

        var listed = await admin.GetJsonAsync<IReadOnlyList<ChannelSettingsDto>>("/api/v1/settings/channels");
        listed.Single(c => c.Channel == NotificationChannel.Email).Configured.ShouldBeFalse();
    }

    /// <summary>A configured Telegram bot is offered to users, and shows as configured to an admin.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_configured_telegram_bot_becomes_available(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await admin.PutJsonAsync<ChannelSettingsDto>("/api/v1/settings/channels/Telegram", new
        {
            configJson = """{"botToken":"abc","botUsername":"everduebot"}""",
            active = true,
        });

        var preferences = await admin.GetJsonAsync<NotificationPreferencesDto>("/api/v1/me/notification-preferences");
        preferences.AvailableChannels.ShouldBe([NotificationChannel.Telegram]);
    }

    /// <summary>
    /// The form has to be editable without re-typing what it cannot show. Non-secret values come
    /// back; secrets come back blank, and blank means "keep the stored one".
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_stored_configuration_round_trips_without_its_secrets(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await admin.PutJsonAsync<ChannelSettingsDto>("/api/v1/settings/channels/Telegram", new
        {
            configJson = """{"botToken":"SECRET-TOKEN","botUsername":"everduebot"}""",
            active = true,
        });

        var listed = await admin.GetJsonAsync<IReadOnlyList<ChannelSettingsDto>>("/api/v1/settings/channels");
        var telegram = listed.Single(c => c.Channel == NotificationChannel.Telegram);

        telegram.RedactedConfigJson.ShouldNotBeNull();
        telegram.RedactedConfigJson!.ShouldContain("everduebot");
        telegram.RedactedConfigJson.ShouldNotContain("SECRET-TOKEN");
    }

    /// <summary>
    /// A configuration that could never send is refused at the point of saving, rather than stored
    /// and then reported as "not configured" with nothing said about why.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_unusable_configuration_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var response = await admin.PutJsonAsync("/api/v1/settings/channels/Telegram", new
        {
            configJson = """{"botUsername":"everduebot"}""",
            active = true,
        });

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).ShouldBe("validation_failed");
    }
}
