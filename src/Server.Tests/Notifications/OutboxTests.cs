using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Notifications;

/// <summary>
/// The outbox: retry, give up, and never let one channel's trouble become anybody else's.
/// </summary>
public class OutboxTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    /// <summary>
    /// Acceptance criterion 2 (the in-app half, and the delivery half through a stand-in provider):
    /// a linked user on a channel gets a delivery row, and it succeeds on the first pass.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_notification_for_a_reachable_user_is_delivered(TestProvider provider)
    {
        var channel = new TestChannel();
        await using var app = await EverdueApp.StartAsync(provider, channel: channel);

        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);
        await app.ReachableOnAsync(memberId, NotificationChannel.Telegram);

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Unload the container",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        (await app.DispatchNotificationsAsync()).ShouldBe(1);

        var delivery = (await app.DeliveriesAsync()).Single();
        delivery.Status.ShouldBe(DeliveryStatus.Sent);
        delivery.SentAt.ShouldNotBeNull();

        // Rendered in the recipient's language from parameters, not from stored text.
        channel.Sent.Single().Message.PlainText.ShouldContain("Unload the container");
    }

    /// <summary>A retryable failure backs off and succeeds later; the attempt count is the evidence.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_retryable_failure_backs_off_and_succeeds_on_a_later_pass(TestProvider provider)
    {
        var channel = new TestChannel().Then(ChannelSendResult.Retry("provider had a moment"));
        await using var app = await EverdueApp.StartAsync(provider, channel: channel);

        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);
        await app.ReachableOnAsync(memberId, NotificationChannel.Telegram);

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Retry me",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        (await app.DispatchNotificationsAsync()).ShouldBe(0);

        var pending = (await app.DeliveriesAsync()).Single();
        pending.Status.ShouldBe(DeliveryStatus.Pending);
        pending.Attempts.ShouldBe(1);
        pending.LastError.ShouldBe("provider had a moment");
        pending.NextAttemptAt.ShouldBeGreaterThan(app.Clock.UtcNow);

        // Nothing happens until the backoff has elapsed — that is what backing off means.
        (await app.DispatchNotificationsAsync()).ShouldBe(0);

        app.Clock.Set(pending.NextAttemptAt.AddSeconds(1));
        (await app.DispatchNotificationsAsync()).ShouldBe(1);

        (await app.DeliveriesAsync()).Single().Status.ShouldBe(DeliveryStatus.Sent);
    }

    /// <summary>
    /// Acceptance criterion 4: a revoked token fails immediately rather than being retried forever,
    /// and the reason is kept for the administrator.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_permanent_failure_is_not_retried(TestProvider provider)
    {
        var channel = new TestChannel { Default = ChannelSendResult.Permanent("Unauthorized: bot token revoked") };
        await using var app = await EverdueApp.StartAsync(provider, channel: channel);

        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);
        await app.ReachableOnAsync(memberId, NotificationChannel.Telegram);

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Doomed",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await app.DispatchNotificationsAsync();

        var delivery = (await app.DeliveriesAsync()).Single();
        delivery.Status.ShouldBe(DeliveryStatus.Failed);
        delivery.Attempts.ShouldBe(1);
        delivery.LastError.ShouldContain("revoked");

        // A failed row is finished: a second pass must not pick it up again.
        channel.Sent.Clear();
        await app.DispatchNotificationsAsync();
        channel.Sent.ShouldBeEmpty();
    }

    /// <summary>Retrying forever is how an outbox turns into log spam. The cap is real.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Retries_give_up_after_the_configured_maximum(TestProvider provider)
    {
        var channel = new TestChannel { Default = ChannelSendResult.Retry("still down") };

        await using var app = await EverdueApp.StartAsync(
            provider,
            new Dictionary<string, string> { ["Notifications:MaxAttempts"] = "3" },
            channel);

        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);
        await app.ReachableOnAsync(memberId, NotificationChannel.Telegram);

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Never arrives",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await app.DispatchNotificationsAsync();
            app.Clock.Set(app.Clock.UtcNow.AddHours(2));
        }

        var delivery = (await app.DeliveriesAsync()).Single();
        delivery.Status.ShouldBe(DeliveryStatus.Failed);
        delivery.Attempts.ShouldBe(3);
    }

    /// <summary>
    /// Acceptance criterion 2's other half: nothing is written for a channel the person is not on,
    /// so an install with no channels configured produces no pending work and no errors at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task With_no_channel_chosen_the_notification_still_exists_and_nothing_is_queued(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);

        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "In-app only",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        (await app.NotificationsForAsync(memberId)).Count.ShouldBe(1);
        (await app.DeliveriesAsync()).ShouldBeEmpty();

        // And a dispatch pass over an empty outbox is a no-op, not an error.
        (await app.DispatchNotificationsAsync()).ShouldBe(0);
    }

    /// <summary>
    /// Acceptance criterion 3: an unconfigured channel is skipped, never failed. "Nothing was owed"
    /// and "something broke" have to look different or the health screen is worthless.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_unconfigured_channel_is_skipped_rather_than_failed(TestProvider provider)
    {
        var channel = new TestChannel { Default = ChannelSendResult.Skipped("Telegram is not configured.") };
        await using var app = await EverdueApp.StartAsync(provider, channel: channel);

        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);
        await app.ReachableOnAsync(memberId, NotificationChannel.Telegram);

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Nowhere to go",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await app.DispatchNotificationsAsync();

        var delivery = (await app.DeliveriesAsync()).Single();
        delivery.Status.ShouldBe(DeliveryStatus.Skipped);
        delivery.Attempts.ShouldBe(0);
    }

    /// <summary>
    /// Retention sweeps read notifications, and **only** read ones: somebody back from three months
    /// away should find the things nobody told them about, not an empty bell.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_sweep_removes_read_notifications_and_keeps_unread_ones(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(
            provider,
            new Dictionary<string, string> { ["Notifications:RetentionDays"] = "30" });

        var admin = await app.SignInAsAdminAsync();
        var member = await app.SignInAsMemberAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        foreach (var title in new[] { "Read later", "Never read" })
        {
            await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
            {
                title,
                ownerUserId = memberId,
                dueDate = app.Clock.UtcNow.AddDays(1),
            });
        }

        // One gets read; the other never does.
        var first = (await app.NotificationsForAsync(memberId))[0];
        await member.PostJsonAsync<UnreadCountDto>("/api/v1/notifications/read", new { ids = new[] { first.Id } });

        // Well past retention, and past the once-a-day sweep interval.
        app.Clock.Set(app.Clock.UtcNow.AddDays(60));
        await app.DispatchNotificationsAsync();

        var remaining = await app.NotificationsForAsync(memberId);

        remaining.Count.ShouldBe(1);
        remaining.Single().ReadAt.ShouldBeNull();
    }

    /// <summary>The bell's own contract: unread count, and marking read.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_bell_counts_unread_and_marking_read_clears_it(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);

        var admin = await app.SignInAsAdminAsync();
        var member = await app.SignInAsMemberAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        foreach (var title in new[] { "One", "Two" })
        {
            await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
            {
                title,
                ownerUserId = memberId,
                dueDate = app.Clock.UtcNow.AddDays(1),
            });
        }

        (await member.GetJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count")).Unread.ShouldBe(2);

        var page = await member.GetJsonAsync<PagedResult<NotificationDto>>("/api/v1/notifications?unreadOnly=true");
        page.TotalCount.ShouldBe(2);

        // The payload is parameters, not rendered text — the client renders it in its own language.
        page.Items[0].Data.ShouldContainKey("title");

        var remaining = await member.PostJsonAsync<UnreadCountDto>("/api/v1/notifications/read", new { ids = (Guid[]?)null });
        remaining.Unread.ShouldBe(0);

        // One person's bell is their own.
        (await admin.GetJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count")).Unread.ShouldBe(0);
    }
}
