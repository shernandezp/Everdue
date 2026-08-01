using System.Net;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// In Progress exists so a manager can split the actionable pile into "being done" and "still
/// queued". It must change nothing else — every count that treats Open as outstanding has to treat
/// this identically, or starting work would quietly improve the numbers.
/// </summary>
public class InProgressTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Work_can_be_started_and_put_back(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var task = await WorkItemTransitionTests.CreateOneOffAsync(app, client);

        (await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/start")).Status
            .ShouldBe(WorkItemStatus.InProgress);

        (await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/reopen")).Status
            .ShouldBe(WorkItemStatus.Open);

        // Straight to done without passing through In Progress stays legal.
        (await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/complete")).Status
            .ShouldBe(WorkItemStatus.Completed);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Starting_work_releases_a_hold(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var task = await WorkItemTransitionTests.CreateOneOffAsync(app, client);

        await client.PostJsonAsync<WorkItemDto>(
            $"/api/v1/workitems/{task.Id}/hold",
            new { reason = nameof(HoldReason.WaitingCustomer) });

        var started = await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/start");

        started.Status.ShouldBe(WorkItemStatus.InProgress);
        started.HoldReason.ShouldBeNull(); // you are no longer waiting on anyone
    }

    /// <summary>The guarantee the whole product rests on: starting is not finishing.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Work_in_progress_is_still_missed_when_its_period_ends(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var (_, occurrence) = await WorkItemTransitionTests.CreateOpenOccurrenceAsync(app, client);
        await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{occurrence.Id}/start");

        app.Clock.Set("2026-08-03T06:00:00Z"); // past the period end
        await app.TickEngineAsync();

        var detail = await client.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{occurrence.Id}");
        detail.Item.Status.ShouldBe(WorkItemStatus.Missed);

        // v2's hold-aging equivalent: the engine records what it interrupted.
        var miss = detail.Events.Last(e => e.ToStatus == WorkItemStatus.Missed);
        miss.FromStatus.ShouldBe(WorkItemStatus.InProgress);
        miss.DataJson.ShouldNotBeNull();
        miss.DataJson!.ShouldContain(nameof(WorkItemStatus.InProgress));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_missed_item_cannot_be_moved_back_into_progress(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var (_, missed) = await WorkItemTransitionTests.CreateMissedOccurrenceAsync(app, client);

        var response = await client.PostJsonAsync($"/api/v1/workitems/{missed.Id}/start");
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var detail = await client.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{missed.Id}");
        detail.AllowedTransitions.ShouldBe([WorkItemStatus.CompletedLate]);
    }

    /// <summary>Starting something must not remove it from a manager's overdue or open counts.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Starting_work_changes_no_report(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var (entityId, occurrence) = await WorkItemTransitionTests.CreateOpenOccurrenceAsync(app, client);

        // Move past the due date but not past the period end: overdue, not yet missed.
        app.Clock.Set("2026-07-28T06:00:00Z");

        var before = await client.GetJsonAsync<ExceptionsReportDto>("/api/v1/reports/exceptions");
        var healthBefore = (await client.GetJsonAsync<PagedResult<EntityHealthRowDto>>("/api/v1/reports/entity-health"))
            .Items.Single(r => r.EntityId == entityId);

        before.Overdue.Count.ShouldBe(1, "the occurrence is past its due date");
        healthBefore.Open.ShouldBe(1);
        healthBefore.Overdue.ShouldBe(1);

        await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{occurrence.Id}/start");

        var after = await client.GetJsonAsync<ExceptionsReportDto>("/api/v1/reports/exceptions");
        var healthAfter = (await client.GetJsonAsync<PagedResult<EntityHealthRowDto>>("/api/v1/reports/entity-health"))
            .Items.Single(r => r.EntityId == entityId);

        after.Overdue.Count.ShouldBe(before.Overdue.Count);
        after.DueToday.Count.ShouldBe(before.DueToday.Count);
        healthAfter.Open.ShouldBe(healthBefore.Open);
        healthAfter.Overdue.ShouldBe(healthBefore.Overdue);

        // And the drill-through behind the number still finds it.
        var overdueList = await client.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems?overdue=true");
        overdueList.Items.ShouldContain(i => i.Id == occurrence.Id && i.Status == WorkItemStatus.InProgress);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task In_progress_work_appears_on_the_board(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var task = await WorkItemTransitionTests.CreateOneOffAsync(app, client);

        await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}/start");

        var board = await client.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems?view=board");
        board.Items.ShouldContain(i => i.Id == task.Id && i.Status == WorkItemStatus.InProgress);

        var filtered = await client.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems?status=inprogress");
        filtered.Items.Single().Id.ShouldBe(task.Id);
    }

    /// <summary>
    /// The timing hole this change surfaced: the engine ticks every few minutes, so an occurrence
    /// finished just after its period ended is still Open when the user clicks. Reading the status
    /// alone recorded that as on-time — a miss erased by a timer boundary.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Completing_after_the_period_ended_is_late_even_before_the_engine_ticks(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var (entityId, occurrence) = await WorkItemTransitionTests.CreateOpenOccurrenceAsync(app, client);

        // One minute past the period end, before any tick has run.
        app.Clock.Set("2026-08-03T05:01:00Z");

        var completed = await client.PostJsonAsync<WorkItemDto>($"/api/v1/workitems/{occurrence.Id}/complete");
        completed.Status.ShouldBe(WorkItemStatus.CompletedLate);

        // And it counts against compliance, exactly as a late completion should.
        var health = (await client.GetJsonAsync<PagedResult<EntityHealthRowDto>>("/api/v1/reports/entity-health"))
            .Items.Single(r => r.EntityId == entityId);

        health.Missed30.ShouldBe(1);
        health.LastActivityAt.ShouldNotBeNull();
    }
}
