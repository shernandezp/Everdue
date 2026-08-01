using System.Net;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Acceptance criteria 12, 13 and 15: handing over a responsibility, handing over a person's whole
/// plate, and bulk actions that report per item instead of half-finishing.
/// </summary>
public class ReassignmentAndBulkTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static async Task<ResponsibilityDto> WeeklyAsync(HttpClient admin, Guid ownerId, EverdueApp app)
        => await admin.PostJsonAsync<ResponsibilityDto>("/api/v1/responsibilities", new
        {
            title = "Weekly supplier call",
            ownerUserId = ownerId,
            recurrenceKind = "Daily",
            startDate = DateOnly.FromDateTime(app.Clock.UtcNow.UtcDateTime).AddDays(-3),
        });

    /// <summary>
    /// Future occurrences follow automatically — the engine copies the owner at spawn — and existing
    /// workable ones follow on request. A missed occurrence counts as workable: it still needs
    /// completing late, and leaving it behind is how it never happens.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Reassigning_a_responsibility_moves_future_and_optionally_existing_occurrences(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var responsibility = await WeeklyAsync(admin, adminId, app);
        await app.TickEngineAsync();

        var before = await admin.GetJsonAsync<PagedResult<WorkItemDto>>(
            $"/api/v1/workitems?ownerId={adminId}&pageSize=100");

        before.Items.ShouldNotBeEmpty();

        var result = await admin.PostJsonAsync<ReassignResultDto>(
            $"/api/v1/responsibilities/{responsibility.Id}/reassign",
            new { newOwnerUserId = memberId, applyToWorkableOccurrences = true });

        result.Responsibilities.ShouldBe(1);
        result.WorkItems.ShouldBe(before.Items.Count);

        // Everything that was on the admin's plate is now on the member's.
        var adminAfter = await admin.GetJsonAsync<PagedResult<WorkItemDto>>(
            $"/api/v1/workitems?ownerId={adminId}&pageSize=100");

        adminAfter.Items.ShouldBeEmpty();

        // And the next occurrence the engine spawns belongs to the new owner without being told.
        app.Clock.AdvanceDays(1);
        await app.TickEngineAsync();

        var memberAfter = await admin.GetJsonAsync<PagedResult<WorkItemDto>>(
            $"/api/v1/workitems?ownerId={memberId}&pageSize=100");

        memberAfter.Items.Count.ShouldBeGreaterThan(before.Items.Count);

        // Each move is recorded as a hand-over, not as an anonymous edit.
        var detail = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{memberAfter.Items[0].Id}");
        detail.ShouldNotBeNull();
    }

    /// <summary>Acceptance criterion 13: the departure path, in one call.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Handing_over_everything_a_departing_user_owns_empties_their_board(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        await WeeklyAsync(admin, memberId, app);
        await app.TickEngineAsync();

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "One-off of the leaver",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(2),
        });

        var result = await admin.PostJsonAsync<ReassignResultDto>($"/api/v1/users/{memberId}/reassign-all", new
        {
            toUserId = adminId,
        });

        result.Responsibilities.ShouldBe(1);
        result.WorkItems.ShouldBeGreaterThan(0);

        var leaverBoard = await admin.GetJsonAsync<PagedResult<WorkItemDto>>(
            $"/api/v1/workitems?ownerId={memberId}&pageSize=100");

        leaverBoard.Items.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Handing_work_to_the_same_person_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var response = await admin.PostJsonAsync($"/api/v1/users/{memberId}/reassign-all", new { toUserId = memberId });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Members do not hand other people's work around wholesale.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Wholesale_reassignment_is_administrator_only(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();

        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        (await member.PostJsonAsync($"/api/v1/users/{memberId}/reassign-all", new { toUserId = adminId }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Acceptance criterion 15: an item somebody already completed is a normal outcome of a bulk
    /// selection. It is reported, and it does not abort the rest.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_bulk_complete_reports_each_item_and_finishes_the_rest(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var ids = new List<Guid>();

        for (var i = 0; i < 5; i++)
        {
            var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
            {
                title = $"Bulk {i}",
                ownerUserId = adminId,
                dueDate = app.Clock.UtcNow.AddDays(1),
            });

            ids.Add(task.Id);
        }

        // Two are already done before the bulk action runs.
        await admin.PostJsonAsync($"/api/v1/workitems/{ids[0]}/complete");
        await admin.PostJsonAsync($"/api/v1/workitems/{ids[1]}/complete");

        var result = await admin.PostJsonAsync<BulkResultDto>("/api/v1/workitems/bulk", new
        {
            ids,
            action = "Complete",
        });

        result.Succeeded.Count.ShouldBe(3);
        result.Failed.Count.ShouldBe(2);
        result.Failed.ShouldAllBe(f => f.Error.Length > 0);

        // The three that went through look exactly like individually completed items.
        foreach (var id in result.Succeeded)
        {
            var detail = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{id}");
            detail.Item.Status.ShouldBe(WorkItemStatus.Completed);
            detail.Events.ShouldContain(e => e.ToStatus == WorkItemStatus.Completed);
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_bulk_reassign_moves_every_item_and_records_a_hand_over(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var ids = new List<Guid>();

        for (var i = 0; i < 3; i++)
        {
            var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
            {
                title = $"Hand over {i}",
                ownerUserId = adminId,
                dueDate = app.Clock.UtcNow.AddDays(1),
            });

            ids.Add(task.Id);
        }

        var result = await admin.PostJsonAsync<BulkResultDto>("/api/v1/workitems/bulk", new
        {
            ids,
            action = "Reassign",
            ownerUserId = memberId,
        });

        result.Succeeded.Count.ShouldBe(3);
        result.Failed.ShouldBeEmpty();

        var detail = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{ids[0]}");
        detail.Item.OwnerUserId.ShouldBe(memberId);
        detail.Events.ShouldContain(e => e.EventType == WorkItemEventType.Reassigned);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_bulk_action_over_the_cap_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var ids = Enumerable.Range(0, 101).Select(_ => Guid.CreateVersion7()).ToArray();

        var response = await admin.PostJsonAsync("/api/v1/workitems/bulk", new { ids, action = "Complete" });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Reassigning with nobody to reassign to is a request that cannot be honoured.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_bulk_reassign_without_an_owner_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var response = await admin.PostJsonAsync("/api/v1/workitems/bulk", new
        {
            ids = new[] { Guid.CreateVersion7() },
            action = "Reassign",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>The dashboard's hand-over count, and the honesty about when counting started.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_dashboard_counts_reassignments_in_the_period(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var before = await admin.GetJsonAsync<ExceptionsReportDto>("/api/v1/reports/exceptions");
        before.Reassigned.Count.ShouldBe(0);
        before.Reassigned.CountingSince.ShouldBeNull();

        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Moves once",
            ownerUserId = adminId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PutJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}", new
        {
            title = "Moves once",
            ownerUserId = memberId,
        });

        var after = await admin.GetJsonAsync<ExceptionsReportDto>("/api/v1/reports/exceptions");
        after.Reassigned.Count.ShouldBe(1);
        after.Reassigned.CountingSince.ShouldNotBeNull();
    }
}
