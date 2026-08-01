using System.Net;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Recurrence;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// The transition matrix as the API actually enforces it, on both providers. Every allowed move
/// succeeds; every disallowed one is refused with 409 and a reason the board can show.
/// </summary>
public class WorkItemTransitionTests
{
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Open_can_be_completed_held_reopened_and_cancelled(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var task = await CreateOneOffAsync(app, client);

        var completed = await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/complete");
        completed.Status.ShouldBe(WorkItemStatus.Completed);
        completed.CompletedAt.ShouldNotBeNull();
        completed.CompletedByUserId.ShouldNotBeNull();

        var reopened = await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/reopen");
        reopened.Status.ShouldBe(WorkItemStatus.Open);
        reopened.CompletedAt.ShouldBeNull();

        var held = await client.PostJsonAsync<WorkItemDto>(
            $"/api/v1/workitems/{task.Id}/hold",
            new { reason = nameof(HoldReason.WaitingSupplier) });
        held.Status.ShouldBe(WorkItemStatus.OnHold);
        held.HoldReason.ShouldBe(HoldReason.WaitingSupplier);

        var released = await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/reopen");
        released.Status.ShouldBe(WorkItemStatus.Open);
        released.HoldReason.ShouldBeNull();

        var cancelled = await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/cancel");
        cancelled.Status.ShouldBe(WorkItemStatus.Cancelled);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_cancelled_task_is_terminal_and_invisible_to_lists(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var task = await CreateOneOffAsync(app, client);
        await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/cancel");

        var reopen = await client.PostJsonAsync($"/api/v1/workitems/{task.Id}/reopen");
        reopen.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var complete = await client.PostJsonAsync($"/api/v1/workitems/{task.Id}/complete");
        complete.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var list = await client.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems");
        list.Items.ShouldNotContain(i => i.Id == task.Id);

        var withCancelled = await client.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems?includeCancelled=true");
        withCancelled.Items.ShouldContain(i => i.Id == task.Id);
    }

    /// <summary>Holding always requires a reason, and choosing "Other" requires explanatory text.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Holding_without_a_reason_is_impossible_and_Other_requires_text(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var task = await CreateOneOffAsync(app, client);

        // No reason at all: the request body cannot even be formed.
        var missing = await client.PostJsonAsync($"/api/v1/workitems/{task.Id}/hold", new { });
        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var other = await client.PostJsonAsync($"/api/v1/workitems/{task.Id}/hold", new { reason = nameof(HoldReason.Other) });
        other.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await other.ProblemCodeAsync()).ShouldBe("validation_failed");

        var withText = await client.PostJsonAsync<WorkItemDto>(
            $"/api/v1/workitems/{task.Id}/hold",
            new { reason = nameof(HoldReason.Other), text = "Waiting for the landlord" });

        withText.Status.ShouldBe(WorkItemStatus.OnHold);
        withText.HoldReasonText.ShouldBe("Waiting for the landlord");
    }

    /// <summary>The miss is never erased.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_missed_occurrence_completes_late_and_stays_counted_as_missed(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var (entityId, occurrence) = await CreateMissedOccurrenceAsync(app, client);

        var before = await client.GetJsonAsync<PagedResult<EntityHealthRowDto>>("/api/v1/reports/entity-health");
        var beforeRow = before.Items.Single(r => r.EntityId == entityId);
        beforeRow.Missed30.ShouldBe(1);
        beforeRow.LastActivityAt.ShouldBeNull();

        var completed = await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{occurrence.Id}/complete");
        completed.Status.ShouldBe(WorkItemStatus.CompletedLate);

        var after = await client.GetJsonAsync<PagedResult<EntityHealthRowDto>>("/api/v1/reports/entity-health");
        var afterRow = after.Items.Single(r => r.EntityId == entityId);

        afterRow.Missed30.ShouldBe(1);           // compliance: still a miss
        afterRow.LastActivityAt.ShouldNotBeNull(); // activity: the work did happen
    }

    /// <summary>
    /// The laundering sequence this forbids: Missed -> complete (CompletedLate) -> reopen (Open)
    /// used to drop the item out of the 30/60/90-day miss counts until the engine's next tick put it
    /// back — or forever, with the engine disabled. Reopening a late completion is refused outright.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_late_completion_cannot_be_reopened_to_erase_the_miss(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var (entityId, occurrence) = await CreateMissedOccurrenceAsync(app, client);

        var completed = await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{occurrence.Id}/complete");
        completed.Status.ShouldBe(WorkItemStatus.CompletedLate);

        var reopen = await client.PostJsonAsync($"/api/v1/workitems/{occurrence.Id}/reopen");
        reopen.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var health = await client.GetJsonAsync<PagedResult<EntityHealthRowDto>>("/api/v1/reports/entity-health");
        health.Items.Single(r => r.EntityId == entityId).Missed30.ShouldBe(1);

        // And the drawer offers no way out either: a late completion is terminal.
        var detail = await client.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{occurrence.Id}");
        detail.AllowedTransitions.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_occurrence_cannot_be_cancelled(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var (_, occurrence) = await CreateOpenOccurrenceAsync(app, client);

        var response = await client.PostJsonAsync($"/api/v1/workitems/{occurrence.Id}/cancel");
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe("conflict");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_occurrence_can_be_rescheduled_inside_its_period_but_never_past_it(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var (_, occurrence) = await CreateOpenOccurrenceAsync(app, client);
        occurrence.PeriodEnd.ShouldNotBeNull();

        var inside = occurrence.PeriodEnd!.Value.AddHours(-1);
        var moved = await client.PostJsonAsync<WorkItemDto>(
            $"/api/v1/workitems/{occurrence.Id}/reschedule",
            new { newDueDate = inside, note = "Client asked for Friday" });

        moved.DueDate.ShouldBe(inside);
        moved.Status.ShouldBe(WorkItemStatus.Open); // rescheduling is an action, not a status

        var past = await client.PostJsonAsync(
            $"/api/v1/workitems/{occurrence.Id}/reschedule",
            new { newDueDate = occurrence.PeriodEnd!.Value.AddSeconds(1) });

        past.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await past.ProblemCodeAsync()).ShouldBe("validation_failed");

        var detail = await client.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{occurrence.Id}");
        detail.Events.ShouldContain(e => e.EventType == WorkItemEventType.Rescheduled);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_one_off_task_can_be_rescheduled_anywhere(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var task = await CreateOneOffAsync(app, client);

        var target = app.Clock.UtcNow.AddDays(90);
        var moved = await client.PostJsonAsync<WorkItemDto>(
            $"/api/v1/workitems/{task.Id}/reschedule",
            new { newDueDate = target });

        moved.DueDate.ShouldBe(target);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Every_mutation_writes_an_event(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var task = await CreateOneOffAsync(app, client);

        await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/hold", new { reason = nameof(HoldReason.WaitingApproval) });
        await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/reopen");
        await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/complete");
        await client.PostJsonAsync<CommentDto>($"/api/v1/workitems/{task.Id}/comments", new { body = "Called them" });

        var detail = await client.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{task.Id}");

        detail.Events.Select(e => e.EventType).ShouldBe(
        [
            WorkItemEventType.Created,
            WorkItemEventType.StatusChanged,
            WorkItemEventType.StatusChanged,
            WorkItemEventType.StatusChanged,
            WorkItemEventType.CommentAdded,
        ]);

        detail.Comments.Count.ShouldBe(1);
        detail.AllowedTransitions.ShouldContain(WorkItemStatus.Open); // undo is offered from Completed
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Occurrences_cannot_be_created_through_the_API(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);

        // The create contract simply has no responsibilityId — the field is unreachable by design.
        var created = await client.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Ad-hoc",
            ownerUserId = adminId,
            dueDate = app.Clock.UtcNow.AddDays(1),
            responsibilityId = Guid.CreateVersion7(),
        });

        created.ResponsibilityId.ShouldBeNull();
    }

    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    internal static async Task<WorkItemDto> CreateOneOffAsync(EverdueApp app, HttpClient client, Guid? entityId = null)
    {
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        return await client.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Send the quarterly report",
            description = "One-off",
            ownerUserId = ownerId,
            entityId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });
    }

    internal static async Task<(Guid EntityId, WorkItemDto Occurrence)> CreateOpenOccurrenceAsync(EverdueApp app, HttpClient client)
        => await CreateOccurrenceAsync(app, client, missed: false);

    internal static async Task<(Guid EntityId, WorkItemDto Occurrence)> CreateMissedOccurrenceAsync(EverdueApp app, HttpClient client)
        => await CreateOccurrenceAsync(app, client, missed: true);

    private static async Task<(Guid EntityId, WorkItemDto Occurrence)> CreateOccurrenceAsync(EverdueApp app, HttpClient client, bool missed)
    {
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var entity = await client.PostJsonAsync<EntityDto>("/api/v1/entities", new { name = $"Acme {Guid.CreateVersion7():N}", type = nameof(EntityType.Customer) });

        // Weekly on Mondays, starting the Monday before "now" (Tuesday 28 July 2026 in Bogota).
        await client.PostJsonAsync<ResponsibilityDto>("/api/v1/responsibilities", new
        {
            title = "Weekly follow-up",
            ownerUserId = ownerId,
            entityId = entity.Id,
            recurrenceKind = nameof(RecurrenceKind.WeeklyOnDays),
            daysOfWeekMask = RecurrenceRule.MaskFor(DayOfWeek.Monday),
            startDate = "2026-07-27",
        });

        await app.TickEngineAsync();

        if (missed)
        {
            // Step past the period end so the next tick records the miss.
            app.Clock.Set("2026-08-03T06:00:00Z");
            await app.TickEngineAsync();
        }

        var wanted = missed ? WorkItemStatus.Missed : WorkItemStatus.Open;
        var items = await client.GetJsonAsync<PagedResult<WorkItemDto>>($"/api/v1/workitems?entityId={entity.Id}&status={wanted}");

        return (entity.Id, items.Items.First(i => i.ResponsibilityId is not null));
    }
}
