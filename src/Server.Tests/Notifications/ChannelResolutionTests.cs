using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Notifications;

/// <summary>
/// Resolution order — tenant credentials, then the system's if this tenant may use them — and the
/// promise that a secret never comes back out of the API.
/// </summary>
public class ChannelResolutionTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private const string TenantBot = """{"botToken":"tenant-token","botUsername":"tenantbot"}""";
    private const string SystemBot = """{"botToken":"system-token","botUsername":"systembot"}""";

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Tenant_credentials_win_over_the_systems(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);

        await app.ScopedAsync(async services =>
        {
            var resolver = services.GetRequiredService<IChannelSettingsResolver>();
            var tenantId = services.GetRequiredService<ITenantContext>().TenantId;

            await resolver.SaveAsync(ChannelSettings.SystemScope, NotificationChannel.Telegram, SystemBot, active: true);
            await resolver.SaveAsync(tenantId, NotificationChannel.Telegram, TenantBot, active: true);

            var resolved = await resolver.ResolveAsync(NotificationChannel.Telegram);

            resolved.ShouldNotBeNull();
            resolved!.Read<TelegramChannelConfig>()!.BotToken.ShouldBe("tenant-token");
            resolved.IsSystemScope.ShouldBeFalse();
        });
    }

    /// <summary>
    /// The flag is the whole hosted-plan hook. Off means "bring your own", and turning it on
    /// makes the same send work with no other change.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_system_fallback_applies_only_when_the_tenant_may_use_it(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var resolver = services.GetRequiredService<IChannelSettingsResolver>();
            var tenantId = services.GetRequiredService<ITenantContext>().TenantId;

            await resolver.SaveAsync(ChannelSettings.SystemScope, NotificationChannel.Telegram, SystemBot, active: true);

            // Default for self-host: allowed.
            (await resolver.ResolveAsync(NotificationChannel.Telegram)).ShouldNotBeNull();

            var tenant = await db.Tenants.SingleAsync(t => t.Id == tenantId);
            tenant.CanUseSystemChannels = false;
            await db.SaveChangesAsync();

            (await resolver.ResolveAsync(NotificationChannel.Telegram)).ShouldBeNull();

            tenant.CanUseSystemChannels = true;
            await db.SaveChangesAsync();

            (await resolver.ResolveAsync(NotificationChannel.Telegram)).ShouldNotBeNull();
        });
    }

    /// <summary>
    /// WhatsApp is never shared, whatever the flag says. The sender identity belongs to one business,
    /// and lending it to another company's staff messages is a commercial relationship, not a setting.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task WhatsApp_never_falls_back_to_the_system_scope(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);

        await app.ScopedAsync(async services =>
        {
            var resolver = services.GetRequiredService<IChannelSettingsResolver>();

            await resolver.SaveAsync(
                ChannelSettings.SystemScope,
                NotificationChannel.WhatsApp,
                """{"phoneNumberId":"1","accessToken":"t"}""",
                active: true);

            (await resolver.ResolveAsync(NotificationChannel.WhatsApp)).ShouldBeNull();
        });
    }

    /// <summary>The token is unreadable in the file and never comes back over the API.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Secrets_are_encrypted_at_rest_and_never_returned(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await admin.PutJsonAsync<ChannelSettingsDto>("/api/v1/settings/channels/Telegram", new
        {
            configJson = TenantBot,
            active = true,
        });

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var stored = await db.ChannelSettings.SingleAsync();

            stored.ConfigProtected.ShouldNotContain("tenant-token");
            stored.ConfigProtected.ShouldNotContain("botToken");
        });

        var listed = await admin.GetJsonAsync<IReadOnlyList<ChannelSettingsDto>>("/api/v1/settings/channels");
        var telegram = listed.Single(c => c.Channel == NotificationChannel.Telegram);

        telegram.Configured.ShouldBeTrue();
        telegram.Summary.ShouldBe("@tenantbot");

        // The whole response, not just the summary field.
        var raw = await (await admin.GetAsync("/api/v1/settings/channels")).Content.ReadAsStringAsync();
        raw.ShouldNotContain("tenant-token");
    }

    /// <summary>
    /// A secret the screen cannot show cannot be re-typed on every edit, so a blank one keeps the
    /// stored value. Without this, renaming a bot would silently wipe its token.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Saving_without_a_secret_keeps_the_stored_one(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await admin.PutJsonAsync<ChannelSettingsDto>("/api/v1/settings/channels/Telegram", new
        {
            configJson = TenantBot,
            active = true,
        });

        await admin.PutJsonAsync<ChannelSettingsDto>("/api/v1/settings/channels/Telegram", new
        {
            configJson = """{"botUsername":"renamedbot"}""",
            active = true,
        });

        await app.ScopedAsync(async services =>
        {
            var resolver = services.GetRequiredService<IChannelSettingsResolver>();
            var config = (await resolver.ResolveAsync(NotificationChannel.Telegram))!.Read<TelegramChannelConfig>()!;

            config.BotUsername.ShouldBe("renamedbot");
            config.BotToken.ShouldBe("tenant-token");
        });
    }

    /// <summary>Health is derived from the delivery rows, and says out loud where receipts do not exist.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Health_reports_failures_per_channel_and_flags_the_absence_of_receipts(TestProvider provider)
    {
        var channel = new TestChannel { Default = ChannelSendResult.Permanent("token revoked") };
        await using var app = await EverdueApp.StartAsync(provider, channel: channel);

        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);
        await app.ReachableOnAsync(memberId, NotificationChannel.Telegram);

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Will fail",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await app.DispatchNotificationsAsync();

        var health = await admin.GetJsonAsync<IReadOnlyList<ChannelHealthDto>>("/api/v1/settings/channels/health");

        var telegram = health.Single(h => h.Channel == NotificationChannel.Telegram);
        telegram.FailedRecently.ShouldBe(1);
        telegram.LastError.ShouldContain("revoked");
        telegram.DeliveryReceiptsSupported.ShouldBeTrue();

        // Stated rather than implied: WhatsApp has no webhook here, so "sent" means "Meta accepted it".
        health.Single(h => h.Channel == NotificationChannel.WhatsApp).DeliveryReceiptsSupported.ShouldBeFalse();
    }

    /// <summary>Choosing a channel you have no address on is refused, not silently downgraded.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Choosing_a_channel_without_an_address_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();

        var response = await member.PutJsonAsync("/api/v1/me/notification-preferences", new
        {
            channel = "Telegram",
            types = (Dictionary<string, bool>?)null,
        });

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).ShouldBe("validation_failed");
    }
}
