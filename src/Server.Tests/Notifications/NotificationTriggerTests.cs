using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Engine;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Notifications;

/// <summary>
/// The five triggers, and the two rules that decide whether anything is written at all: the type has
/// to be wanted, and a dedupe key wins.
/// </summary>
public class NotificationTriggerTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    /// <summary>
    /// Acceptance criterion 1: assigning work to somebody tells them, and telling yourself about your
    /// own task is noise nobody asked for.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Assigning_work_notifies_the_new_owner_and_not_the_actor(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Check the loading bay",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "My own task",
            ownerUserId = adminId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        var forMember = await app.NotificationsForAsync(memberId);
        var forAdmin = await app.NotificationsForAsync(adminId);

        forMember.Select(n => n.Type).ShouldBe([NotificationType.Assigned]);
        forAdmin.ShouldBeEmpty();
    }

    /// <summary>Handing an item over through the ordinary edit notifies exactly like a create does.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Reassigning_through_an_edit_notifies_the_new_owner(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Vehicle inspection",
            ownerUserId = adminId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PutJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}", new
        {
            title = "Vehicle inspection",
            ownerUserId = memberId,
        });

        (await app.NotificationsForAsync(memberId)).Select(n => n.Type).ShouldBe([NotificationType.Assigned]);
    }

    /// <summary>Somebody else parking your work is the kind of thing you otherwise find out about a week late.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Putting_someone_elses_item_on_hold_tells_the_owner(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var member = await app.SignInAsMemberAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Supplier callback",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PostJsonAsync($"/api/v1/workitems/{task.Id}/hold", new { reason = "WaitingSupplier" });

        var notifications = await app.NotificationsForAsync(memberId);
        notifications.ShouldContain(n => n.Type == NotificationType.PutOnHold);

        // Holding your own item announces nothing.
        var own = await member.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Mine",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await member.PostJsonAsync($"/api/v1/workitems/{own.Id}/hold", new { reason = "WaitingCustomer" });

        (await app.NotificationsForAsync(memberId))
            .Count(n => n.Type == NotificationType.PutOnHold)
            .ShouldBe(1);
    }

    /// <summary>Mentions are picked, not parsed: the ids come from the client and are validated here.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_mention_notifies_the_person_mentioned(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Quote follow-up",
            ownerUserId = adminId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PostJsonAsync<CommentDto>($"/api/v1/workitems/{task.Id}/comments", new
        {
            body = "@Member can you take a look?",
            mentionedUserIds = new[] { memberId, adminId },
        });

        var forMember = await app.NotificationsForAsync(memberId);
        forMember.Select(n => n.Type).ShouldBe([NotificationType.Mentioned]);

        // Mentioning yourself is not a notification.
        (await app.NotificationsForAsync(adminId)).ShouldBeEmpty();
    }

    /// <summary>A type switched off produces nothing at all — not a suppressed delivery, nothing.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_switched_off_type_produces_no_notification_and_no_delivery(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var member = await app.SignInAsMemberAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        await member.PutJsonAsync<NotificationPreferencesDto>("/api/v1/me/notification-preferences", new
        {
            channel = (string?)null,
            types = new Dictionary<string, bool> { ["Assigned"] = false },
        });

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Something they muted",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        (await app.NotificationsForAsync(memberId)).ShouldBeEmpty();

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            (await db.NotificationDeliveries.CountAsync()).ShouldBe(0);
        });
    }

    /// <summary>
    /// Acceptance criterion 6: two reminder runs on the same local day — and a restart between them —
    /// produce one notification per due item. The dedupe key is the whole mechanism; there is no
    /// "last run" marker anywhere, exactly as the occurrence engine has none.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Due_today_reminders_are_sent_once_per_day_however_often_the_service_runs(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        // 15:00 UTC is 10:00 in Bogota, past the tenant's 08:00 reminder hour.
        var todayLocalEnd = app.Clock.UtcNow.Date.AddDays(1).AddSeconds(-1);

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Due today",
            ownerUserId = memberId,
            dueDate = todayLocalEnd,
        });

        await app.RunRemindersAsync();
        await app.RunRemindersAsync();

        var dueToday = (await app.NotificationsForAsync(memberId))
            .Where(n => n.Type == NotificationType.DueToday)
            .ToArray();

        dueToday.Length.ShouldBe(1);
    }

    /// <summary>
    /// Acceptance criterion 7: coming back from a fortnight of downtime records every miss and
    /// announces only the recent ones. The ledger is untouched; the interruption is what is capped.
    /// </summary>
    [Fact]
    public async Task Catch_up_after_downtime_records_every_miss_but_announces_only_recent_ones()
    {
        await using var harness = await EngineHarness.CreateAsync();
        harness.Clock.Set("2026-07-01T13:00:00Z");

        harness.AddResponsibility(RecurrenceKind.Daily, DateOnly.Parse("2026-06-15"));
        await harness.Db.SaveChangesAsync();

        // Two weeks later, in one tick.
        harness.Clock.Set("2026-07-15T13:00:00Z");
        await harness.Engine().TickAsync();

        var missed = await harness.Db.WorkItems.CountAsync(w => w.Status == WorkItemStatus.Missed);
        missed.ShouldBeGreaterThan(20);

        var announced = harness.Notifications.Of(NotificationType.Missed);

        // One day's worth of period boundaries falls inside the 24-hour window, not a month's worth.
        announced.Count.ShouldBeLessThan(3);
        announced.Count.ShouldBeGreaterThan(0);
    }
}
