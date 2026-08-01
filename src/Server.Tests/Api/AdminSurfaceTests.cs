using System.Net;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// The two settings v1.5 introduced that somebody has to be able to change, and the one contact
/// detail an administrator maintains on another person's behalf. A feature nobody can configure is
/// not a feature.
/// </summary>
public class AdminSurfaceTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_reminder_hour_and_the_system_channel_flag_are_editable(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var before = await admin.GetJsonAsync<TenantSettingsDto>("/api/v1/settings/tenant");
        before.ReminderHourLocal.ShouldBe(8);
        before.CanUseSystemChannels.ShouldBeTrue();

        var after = await admin.PutJsonAsync<TenantSettingsDto>("/api/v1/settings/tenant", new
        {
            name = before.Name,
            timeZoneId = before.TimeZoneId,
            digestHourLocal = before.DigestHourLocal,
            defaultLanguage = before.DefaultLanguage,
            reminderHourLocal = 14,
            canUseSystemChannels = false,
        });

        after.ReminderHourLocal.ShouldBe(14);
        after.CanUseSystemChannels.ShouldBeFalse();

        // And it is the value the engine will actually read.
        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var tenant = await db.Tenants.SingleAsync();

            tenant.ReminderHourLocal.ShouldBe(14);
            tenant.CanUseSystemChannels.ShouldBeFalse();
        });
    }

    /// <summary>
    /// The tenant DTO is served from two places — <c>/settings/tenant</c> and, nested, <c>/auth/me</c> — and the
    /// SPA reads most of it from the second, because that is the one every role can reach.
    ///
    /// <para>They must be the same object. When <c>/auth/me</c> built its own copy by calling the constructor
    /// positionally, every field added afterwards silently arrived as its C# default there: the reminder hour
    /// read 8 however it was configured, and the demo-mode flag read false on an install full of demo data. The
    /// symptom is invisible — a plausible value, not an error — so this compares the whole record rather than
    /// naming fields, and a future field is covered without anybody remembering to come back here.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Me_and_the_settings_endpoint_report_the_same_tenant(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        // Moved off every default first, so "they agree" cannot be satisfied by both being wrong.
        await admin.PutJsonAsync<TenantSettingsDto>("/api/v1/settings/tenant", new
        {
            name = "Configured workspace",
            timeZoneId = "America/Bogota",
            digestHourLocal = 5,
            defaultLanguage = Languages.English,
            reminderHourLocal = 19,
            canUseSystemChannels = false,
        });

        var settings = await admin.GetJsonAsync<TenantSettingsDto>("/api/v1/settings/tenant");
        var me = await admin.GetJsonAsync<CurrentUserDto>("/api/v1/auth/me");

        me.Tenant.ShouldBe(settings);
    }

    /// <summary>
    /// WhatsApp has no linking flow, so without this field the channel is unreachable: there would
    /// be no way for anybody's number to get into the system at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_administrator_can_set_a_whatsapp_number(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var updated = await admin.PutJsonAsync<UserDto>($"/api/v1/users/{memberId}", new
        {
            displayName = "Member",
            role = "Member",
            active = true,
            whatsAppPhoneE164 = "+57 300 111 2233",
        });

        // Normalised: spaces out, the number kept as Meta expects it.
        updated.WhatsAppPhoneE164.ShouldBe("+573001112233");

        var preferences = await (await app.SignInAsMemberAsync())
            .GetJsonAsync<NotificationPreferencesDto>("/api/v1/me/notification-preferences");

        preferences.WhatsAppPhoneE164.ShouldBe("+573001112233");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_number_that_is_not_e164_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var response = await admin.PutJsonAsync($"/api/v1/users/{memberId}", new
        {
            displayName = "Member",
            role = "Member",
            active = true,
            whatsAppPhoneE164 = "300-111-2233",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>A member picking an owner has no business reading colleagues' phone numbers.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Members_do_not_see_colleagues_numbers(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var member = await app.SignInAsMemberAsync();
        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);

        await admin.PutJsonAsync<UserDto>($"/api/v1/users/{adminId}", new
        {
            displayName = "Administrator",
            role = "Admin",
            active = true,
            whatsAppPhoneE164 = "+573009998877",
        });

        var asAdmin = await admin.GetJsonAsync<IReadOnlyList<UserDto>>("/api/v1/users");
        asAdmin.Single(u => u.Id == adminId).WhatsAppPhoneE164.ShouldBe("+573009998877");

        var asMember = await member.GetJsonAsync<IReadOnlyList<UserDto>>("/api/v1/users");
        asMember.ShouldAllBe(u => u.WhatsAppPhoneE164 == null);
    }
}
