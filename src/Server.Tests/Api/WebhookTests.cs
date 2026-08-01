using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Webhooks;
using Everdue.Server.Domain;
using Everdue.Server.Engine;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Infrastructure.Webhooks;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Webhooks, driven against a stub receiver.
///
/// The two guarantees worth pinning are that a delivery can never exist for work that rolled back, and that a
/// broken subscriber changes nothing about a request or the ledger.
/// </summary>
public class WebhookTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static Task<EverdueApp> StartAsync(TestProvider provider, StubWebhookReceiver receiver)
        => EverdueApp.StartWithServicesAsync(provider, services =>
        {
            // Replaces the primary handler of the sender's typed client, leaving everything else — signing,
            // timeouts, error classification — exactly as it ships.
            services.Configure<HttpClientFactoryOptions>(
                nameof(WebhookSender),
                options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = receiver));
        });

    private static async Task<(Guid Id, string Secret)> ASubscriptionAsync(
        HttpClient admin,
        params WebhookEventType[] events)
    {
        var created = await admin.PostJsonAsync<CreatedWebhookDto>("/api/v1/webhooks", new
        {
            url = "https://receiver.test/everdue",
            eventTypes = (events.Length > 0 ? events : [WebhookEventType.WorkItemCompleted]).Select(e => e.ToString()).ToArray(),
        });

        return (created.Subscription.Id, created.Secret);
    }

    private static Task<int> DrainAsync(EverdueApp app)
        => app.ScopedAsync(services =>
            services.GetRequiredService<WebhookDispatcherService>().RunOnceAsync(CancellationToken.None));

    private static async Task<Guid> ATaskAsync(EverdueApp app, HttpClient admin, string title = "A task")
    {
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var created = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title,
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        return created.Id;
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_completion_is_delivered_and_its_signature_verifies(TestProvider provider)
    {
        var receiver = new StubWebhookReceiver();
        await using var app = await StartAsync(provider, receiver);
        var admin = await app.SignInAsAdminAsync();

        var (_, secret) = await ASubscriptionAsync(admin, WebhookEventType.WorkItemCompleted);

        var id = await ATaskAsync(app, admin, "Send the quotation");
        await (await admin.PostAsync($"/api/v1/workitems/{id}/complete", null)).ShouldBeSuccessAsync();

        (await DrainAsync(app)).ShouldBe(1);

        var delivered = receiver.Received.Single();

        var payload = JsonDocument.Parse(delivered.Body).RootElement;
        payload.GetProperty("type").GetString().ShouldBe("workitem.completed");
        payload.GetProperty("data").GetProperty("title").GetString().ShouldBe("Send the quotation");
        payload.GetProperty("data").GetProperty("late").GetBoolean().ShouldBeFalse();

        // Verified independently of the signing code, over "{id}.{timestamp}.{body}" exactly as a subscriber would.
        var eventId = delivered.Headers[WebhookSignature.IdHeader];
        var timestamp = delivered.Headers[WebhookSignature.TimestampHeader];

        var expected = Convert.ToBase64String(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes($"{eventId}.{timestamp}.{delivered.Body}")));

        delivered.Headers[WebhookSignature.SignatureHeader].ShouldBe($"v1,{expected}");

        // The header id is the event id in the payload — that pairing is what makes deduplication possible.
        payload.GetProperty("id").GetString().ShouldBe(eventId);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Only_subscribed_events_are_delivered(TestProvider provider)
    {
        var receiver = new StubWebhookReceiver();
        await using var app = await StartAsync(provider, receiver);
        var admin = await app.SignInAsAdminAsync();

        await ASubscriptionAsync(admin, WebhookEventType.WorkItemOnHold);

        var id = await ATaskAsync(app, admin);

        // Created and completed are not subscribed; on-hold is.
        await admin.PostJsonAsync($"/api/v1/workitems/{id}/hold", new { reason = nameof(HoldReason.WaitingCustomer) });
        await DrainAsync(app);

        receiver.Received.Count.ShouldBe(1);
        JsonDocument.Parse(receiver.Received[0].Body).RootElement.GetProperty("type").GetString().ShouldBe("workitem.onhold");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_retry_reuses_the_same_webhook_id_and_backs_off(TestProvider provider)
    {
        var receiver = new StubWebhookReceiver().Answering(HttpStatusCode.InternalServerError);
        await using var app = await StartAsync(provider, receiver);
        var admin = await app.SignInAsAdminAsync();

        await ASubscriptionAsync(admin, WebhookEventType.WorkItemCompleted);

        var id = await ATaskAsync(app, admin);
        await admin.PostAsync($"/api/v1/workitems/{id}/complete", null);

        (await DrainAsync(app)).ShouldBe(0);

        // Still pending, and not due again yet: the backoff is real, not a busy loop.
        var pendingNow = await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var delivery = await db.WebhookDeliveries.AsNoTracking().SingleAsync();

            delivery.Status.ShouldBe(DeliveryStatus.Pending);
            delivery.Attempts.ShouldBe(1);
            delivery.ResponseStatus.ShouldBe(500);

            return delivery.NextAttemptAt;
        });

        pendingNow.ShouldBeGreaterThan(app.Clock.UtcNow);

        app.Clock.Advance(TimeSpan.FromHours(2));
        (await DrainAsync(app)).ShouldBe(1);

        receiver.Received.Count.ShouldBe(2);
        receiver.Received[0].Headers[WebhookSignature.IdHeader]
            .ShouldBe(receiver.Received[1].Headers[WebhookSignature.IdHeader]);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_404_fails_immediately_without_retrying(TestProvider provider)
    {
        var receiver = new StubWebhookReceiver { Default = HttpStatusCode.NotFound };
        await using var app = await StartAsync(provider, receiver);
        var admin = await app.SignInAsAdminAsync();

        await ASubscriptionAsync(admin, WebhookEventType.WorkItemCompleted);

        var id = await ATaskAsync(app, admin);
        await admin.PostAsync($"/api/v1/workitems/{id}/complete", null);

        await DrainAsync(app);

        // Retrying a 404 for an hour is how an outbox becomes log spam.
        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var delivery = await db.WebhookDeliveries.AsNoTracking().SingleAsync();

            delivery.Status.ShouldBe(DeliveryStatus.Failed);
            delivery.Attempts.ShouldBe(1);
        });
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Repeated_failure_disables_the_subscription_and_a_put_brings_it_back(TestProvider provider)
    {
        var receiver = new StubWebhookReceiver { Unreachable = true };

        await using var app = await EverdueApp.StartWithServicesAsync(
            provider,
            services => services.Configure<HttpClientFactoryOptions>(
                nameof(WebhookSender),
                options => options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = receiver)),
            new Dictionary<string, string>
            {
                ["Webhooks:MaxConsecutiveFailures"] = "2",
                ["Webhooks:MaxAttempts"] = "1",
            });

        var admin = await app.SignInAsAdminAsync();
        var (subscriptionId, _) = await ASubscriptionAsync(admin, WebhookEventType.WorkItemCompleted);

        for (var index = 0; index < 2; index++)
        {
            var id = await ATaskAsync(app, admin, $"Task {index}");
            await admin.PostAsync($"/api/v1/workitems/{id}/complete", null);
            await DrainAsync(app);
        }

        var disabled = (await admin.GetJsonAsync<IReadOnlyList<WebhookSubscriptionDto>>("/api/v1/webhooks")).Single();
        disabled.Active.ShouldBeFalse();
        disabled.DisabledAt.ShouldNotBeNull();
        disabled.ConsecutiveFailures.ShouldBeGreaterThanOrEqualTo(2);

        var health = (await admin.GetJsonAsync<IReadOnlyList<WebhookHealthDto>>("/api/v1/webhooks/health")).Single();
        health.Active.ShouldBeFalse();
        health.Failed24h.ShouldBe(2);

        // Re-enabling is a decision somebody makes, and it starts the failure count over.
        var revived = await admin.PutJsonAsync<WebhookSubscriptionDto>($"/api/v1/webhooks/{subscriptionId}", new
        {
            url = disabled.Url,
            eventTypes = new[] { nameof(WebhookEventType.WorkItemCompleted) },
            active = true,
        });

        revived.Active.ShouldBeTrue();
        revived.ConsecutiveFailures.ShouldBe(0);
        revived.DisabledAt.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_dead_receiver_changes_nothing_about_a_request_or_the_ledger(TestProvider provider)
    {
        var receiver = new StubWebhookReceiver { Unreachable = true };
        await using var app = await StartAsync(provider, receiver);
        var admin = await app.SignInAsAdminAsync();

        await ASubscriptionAsync(admin, WebhookEventType.WorkItemCompleted, WebhookEventType.WorkItemCreated);

        var id = await ATaskAsync(app, admin);

        // The request that raises the event succeeds regardless — nothing about telling another system may ever be
        // able to fail the work.
        var response = await admin.PostAsync($"/api/v1/workitems/{id}/complete", null);
        await response.ShouldBeSuccessAsync();

        await DrainAsync(app);

        var detail = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{id}");
        detail.Item.Status.ShouldBe(WorkItemStatus.Completed);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_engine_honours_the_catch_up_guards(TestProvider provider)
    {
        var receiver = new StubWebhookReceiver();
        await using var app = await StartAsync(provider, receiver);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        await ASubscriptionAsync(admin, WebhookEventType.WorkItemMissed, WebhookEventType.WorkItemCreated);

        // A daily responsibility starting a fortnight ago: the first tick records fourteen days of misses.
        await admin.PostJsonAsync<ResponsibilityDto>("/api/v1/responsibilities", new
        {
            title = "Daily cash count",
            ownerUserId = ownerId,
            recurrenceKind = nameof(RecurrenceKind.Daily),
            startDate = DateOnly.FromDateTime(app.Clock.UtcNow.UtcDateTime).AddDays(-14),
        });

        await app.TickEngineAsync();

        var occurrences = await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            return await db.WorkItems.CountAsync(w => w.ResponsibilityId != null);
        });

        // The ledger has every row…
        occurrences.ShouldBeGreaterThan(10);

        var deliveries = await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            return await db.WebhookDeliveries.AsNoTracking().ToListAsync();
        });

        // …and only the last day's worth was announced, plus the one still-open occurrence.
        deliveries.Count(d => d.EventType == WebhookEventType.WorkItemMissed).ShouldBeLessThanOrEqualTo(2);
        deliveries.Count(d => d.EventType == WebhookEventType.WorkItemCreated).ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_payload_carries_no_description_comment_or_custom_field(TestProvider provider)
    {
        var receiver = new StubWebhookReceiver();
        await using var app = await StartAsync(provider, receiver);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var field = await admin.PostJsonAsync<EntityFieldDefDto>("/api/v1/entity-fields", new
        {
            entityType = nameof(EntityType.Customer),
            name = "Account manager",
            fieldType = nameof(EntityFieldType.Text),
        });

        var entity = await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "Acme Ltd",
            type = nameof(EntityType.Customer),
            customFields = new Dictionary<string, string> { [field.Id.ToString()] = "SECRET-MANAGER" },
        });

        await ASubscriptionAsync(admin, WebhookEventType.WorkItemCompleted);

        var created = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Visible title",
            description = "PRIVATE-DESCRIPTION",
            ownerUserId = ownerId,
            entityId = entity.Id,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PostJsonAsync($"/api/v1/workitems/{created.Id}/comments", new { body = "PRIVATE-COMMENT" });
        await admin.PostAsync($"/api/v1/workitems/{created.Id}/complete", null);

        await DrainAsync(app);

        var body = receiver.Received.Single().Body;

        body.ShouldContain("Visible title");
        body.ShouldContain("Acme Ltd");
        body.ShouldNotContain("PRIVATE-DESCRIPTION");
        body.ShouldNotContain("PRIVATE-COMMENT");
        body.ShouldNotContain("SECRET-MANAGER");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_test_button_queues_one_signed_ping(TestProvider provider)
    {
        var receiver = new StubWebhookReceiver();
        await using var app = await StartAsync(provider, receiver);
        var admin = await app.SignInAsAdminAsync();

        var (subscriptionId, _) = await ASubscriptionAsync(admin);

        await (await admin.PostAsync($"/api/v1/webhooks/{subscriptionId}/test", null)).ShouldBeSuccessAsync();
        (await DrainAsync(app)).ShouldBe(1);

        JsonDocument.Parse(receiver.Received.Single().Body).RootElement
            .GetProperty("type").GetString().ShouldBe("ping");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_http_url_is_refused_unless_it_has_been_allowed(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var refused = await admin.PostJsonAsync("/api/v1/webhooks", new
        {
            url = "http://receiver.test/everdue",
            eventTypes = new[] { nameof(WebhookEventType.WorkItemMissed) },
        });

        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Ping is not subscribable: it is what the test button sends, not something to ask for.
        var pingOnly = await admin.PostJsonAsync("/api/v1/webhooks", new
        {
            url = "https://receiver.test/everdue",
            eventTypes = new[] { nameof(WebhookEventType.Ping) },
        });

        pingOnly.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_signing_secret_is_never_returned_after_creation(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var (_, secret) = await ASubscriptionAsync(admin);

        var listed = await admin.GetAsync("/api/v1/webhooks");
        var body = await listed.Content.ReadAsStringAsync();

        body.ShouldNotContain(secret);

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var stored = await db.WebhookSubscriptions.AsNoTracking().SingleAsync();

            // Encrypted with the data-protection key ring, like the channel credentials.
            stored.SecretProtected.ShouldNotContain(secret);
        });
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Webhook_management_is_administrator_only(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();

        (await member.GetAsync("/api/v1/webhooks")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.GetAsync("/api/v1/webhooks/health")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
