using System.Net;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Checklists, and the two completion rules built on them.
///
/// The property worth protecting most carefully is that an occurrence's checklist is a <em>snapshot</em>: editing a
/// template must never rewrite what an occurrence was asked to do.
/// </summary>
public class ChecklistTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static object Template(params (string Text, bool Required)[] items)
        => new { items = items.Select(item => new { text = item.Text, required = item.Required }).ToArray() };

    private static async Task<Guid> AResponsibilityAsync(EverdueApp app, HttpClient client, bool requireChecklist = false)
    {
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var created = await client.PostJsonAsync<ResponsibilityDto>("/api/v1/responsibilities", new
        {
            title = "Daily line check",
            ownerUserId = ownerId,
            recurrenceKind = nameof(RecurrenceKind.Daily),
            startDate = DateOnly.FromDateTime(app.Clock.UtcNow.UtcDateTime),
            requireChecklistToComplete = requireChecklist,
        });

        return created.Id;
    }

    private static Task<Guid> TheOccurrenceAsync(EverdueApp app, Guid responsibilityId) => app.ScopedAsync(async services =>
    {
        var db = services.GetRequiredService<EverdueDbContext>();
        return await db.WorkItems.Where(w => w.ResponsibilityId == responsibilityId).Select(w => w.Id).FirstAsync();
    });

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_template_is_copied_onto_every_occurrence_in_order(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var responsibilityId = await AResponsibilityAsync(app, admin);

        await (await admin.PutJsonAsync(
            $"/api/v1/responsibilities/{responsibilityId}/checklist-template",
            Template(("Read the meter", true), ("Check the seal", false), ("Sign the log", true))))
            .ShouldBeSuccessAsync();

        await app.TickEngineAsync();

        var occurrenceId = await TheOccurrenceAsync(app, responsibilityId);
        var checklist = await admin.GetJsonAsync<IReadOnlyList<ChecklistItemDto>>($"/api/v1/workitems/{occurrenceId}/checklist");

        checklist.Count.ShouldBe(3);
        checklist.Select(item => item.Text).ShouldBe(["Read the meter", "Check the seal", "Sign the log"]);
        checklist.Select(item => item.Required).ShouldBe([true, false, true]);
        checklist.All(item => item.CheckedAt is null).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Editing_the_template_leaves_existing_occurrences_untouched(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var responsibilityId = await AResponsibilityAsync(app, admin);

        await admin.PutJsonAsync(
            $"/api/v1/responsibilities/{responsibilityId}/checklist-template",
            Template(("Original step", true)));

        await app.TickEngineAsync();
        var occurrenceId = await TheOccurrenceAsync(app, responsibilityId);

        // The whole reason the occurrence's list is a copy rather than a foreign key.
        await admin.PutJsonAsync(
            $"/api/v1/responsibilities/{responsibilityId}/checklist-template",
            Template(("Completely different step", false), ("And another", true)));

        var checklist = await admin.GetJsonAsync<IReadOnlyList<ChecklistItemDto>>($"/api/v1/workitems/{occurrenceId}/checklist");

        checklist.Single().Text.ShouldBe("Original step");
        checklist.Single().Required.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_double_tick_produces_one_occurrence_and_one_checklist(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var responsibilityId = await AResponsibilityAsync(app, admin);

        await admin.PutJsonAsync(
            $"/api/v1/responsibilities/{responsibilityId}/checklist-template",
            Template(("Step one", true), ("Step two", true)));

        await app.TickEngineAsync();
        await app.TickEngineAsync();

        await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();

            var occurrences = await db.WorkItems.CountAsync(w => w.ResponsibilityId == responsibilityId);
            occurrences.ShouldBe(1);

            // The unique index that makes a racing tick harmless covers the checklist too, because both are
            // written in the same SaveChanges.
            var lines = await db.ChecklistItems.CountAsync();
            lines.ShouldBe(2);
        });
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_ad_hoc_item_is_never_required_whatever_is_asked(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Collect signatures",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        // The request carries only text — the point is that there is no way to ask for `required` at all, so a
        // gate cannot be invented mid-period on somebody else's item.
        var added = await admin.PostJsonAsync<ChecklistItemDto>(
            $"/api/v1/workitems/{task.Id}/checklist",
            new { text = "Also photograph the pallet" });

        added.Required.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Checking_is_refused_once_the_item_is_completed_and_survives_a_reopen(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Weekly report",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        var line = await admin.PostJsonAsync<ChecklistItemDto>(
            $"/api/v1/workitems/{task.Id}/checklist",
            new { text = "Attach the numbers" });

        await (await admin.PostAsync($"/api/v1/workitems/{task.Id}/checklist/{line.Id}/check", null)).ShouldBeSuccessAsync();
        await (await admin.PostAsync($"/api/v1/workitems/{task.Id}/complete", null)).ShouldBeSuccessAsync();

        var refused = await admin.PostAsync($"/api/v1/workitems/{task.Id}/checklist/{line.Id}/uncheck", null);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await (await admin.PostAsync($"/api/v1/workitems/{task.Id}/reopen", null)).ShouldBeSuccessAsync();

        // Reopening makes the list editable again but does *not* clear what was ticked: the work really was done.
        var after = await admin.GetJsonAsync<IReadOnlyList<ChecklistItemDto>>($"/api/v1/workitems/{task.Id}/checklist");
        after.Single().CheckedAt.ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Completion_is_refused_until_the_required_items_are_checked(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var responsibilityId = await AResponsibilityAsync(app, admin, requireChecklist: true);

        await admin.PutJsonAsync(
            $"/api/v1/responsibilities/{responsibilityId}/checklist-template",
            Template(("Required one", true), ("Required two", true), ("Optional", false)));

        await app.TickEngineAsync();
        var occurrenceId = await TheOccurrenceAsync(app, responsibilityId);

        var detail = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{occurrenceId}");
        detail.CompletionRequirements.ShouldNotBeNull();
        detail.CompletionRequirements!.RequiredChecklistOpen.ShouldBe(2);

        // Completed is still an allowed transition — the server refuses with a reason rather than pretending the
        // move does not exist.
        detail.AllowedTransitions.ShouldContain(WorkItemStatus.Completed);

        var blocked = await admin.PostAsync($"/api/v1/workitems/{occurrenceId}/complete", null);
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await blocked.Content.ReadAsStringAsync()).ShouldContain("2 required checklist items");

        var checklist = await admin.GetJsonAsync<IReadOnlyList<ChecklistItemDto>>($"/api/v1/workitems/{occurrenceId}/checklist");

        foreach (var required in checklist.Where(item => item.Required))
        {
            await admin.PostAsync($"/api/v1/workitems/{occurrenceId}/checklist/{required.Id}/check", null);
        }

        // The optional one is still unchecked, and that is fine.
        await (await admin.PostAsync($"/api/v1/workitems/{occurrenceId}/complete", null)).ShouldBeSuccessAsync();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Progress_travels_on_the_list_projection(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var responsibilityId = await AResponsibilityAsync(app, admin);

        await admin.PutJsonAsync(
            $"/api/v1/responsibilities/{responsibilityId}/checklist-template",
            Template(("One", false), ("Two", false)));

        await app.TickEngineAsync();
        var occurrenceId = await TheOccurrenceAsync(app, responsibilityId);

        var checklist = await admin.GetJsonAsync<IReadOnlyList<ChecklistItemDto>>($"/api/v1/workitems/{occurrenceId}/checklist");
        await admin.PostAsync($"/api/v1/workitems/{occurrenceId}/checklist/{checklist[0].Id}/check", null);

        var list = await admin.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems?occurrences=true");
        var row = list.Items.Single(item => item.Id == occurrenceId);

        row.ChecklistTotal.ShouldBe(2);
        row.ChecklistChecked.ShouldBe(1);

        // A one-off with no checklist reports nulls, so the UI shows no badge rather than "0/0".
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "No checklist here",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        task.ChecklistTotal.ShouldBeNull();
        task.ChecklistChecked.ShouldBeNull();
    }

    /// <summary>
    /// The half that is easy to forget: progress shows on the entity timeline too, because an
    /// entity's inspection history is exactly where somebody asks how much of each check was actually done.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Progress_travels_on_the_entity_timeline(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var entity = await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "Cold room",
            type = nameof(EntityType.Equipment),
        });

        var responsibility = await admin.PostJsonAsync<ResponsibilityDto>("/api/v1/responsibilities", new
        {
            title = "Temperature log",
            ownerUserId = ownerId,
            entityId = entity.Id,
            recurrenceKind = nameof(RecurrenceKind.Daily),
            startDate = DateOnly.FromDateTime(app.Clock.UtcNow.UtcDateTime),
        });

        await admin.PutJsonAsync(
            $"/api/v1/responsibilities/{responsibility.Id}/checklist-template",
            Template(("Morning reading", true), ("Evening reading", true), ("Door seal", false)));

        await app.TickEngineAsync();

        var occurrenceId = await TheOccurrenceAsync(app, responsibility.Id);
        var checklist = await admin.GetJsonAsync<IReadOnlyList<ChecklistItemDto>>($"/api/v1/workitems/{occurrenceId}/checklist");

        await admin.PostAsync($"/api/v1/workitems/{occurrenceId}/checklist/{checklist[0].Id}/check", null);

        var timeline = await admin.GetJsonAsync<EntityTimelineDto>($"/api/v1/reports/entities/{entity.Id}/timeline");
        var row = timeline.Items.Single(item => item.WorkItemId == occurrenceId);

        row.ChecklistTotal.ShouldBe(3);
        row.ChecklistChecked.ShouldBe(1);

        // A one-off with no checklist reports nulls here too, so the badge is absent rather than "0/0".
        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Replace the thermometer",
            ownerUserId = ownerId,
            entityId = entity.Id,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        var again = await admin.GetJsonAsync<EntityTimelineDto>($"/api/v1/reports/entities/{entity.Id}/timeline");
        var taskRow = again.Items.Single(item => item.WorkItemId == task.Id);

        taskRow.ChecklistTotal.ShouldBeNull();
        taskRow.ChecklistChecked.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_one_off_task_is_never_gated(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Ad-hoc job",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PostJsonAsync<ChecklistItemDto>($"/api/v1/workitems/{task.Id}/checklist", new { text = "A note to self" });

        var detail = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{task.Id}");

        // No responsibility means no rule to apply, whatever is on the list.
        detail.CompletionRequirements.ShouldBeNull();

        await (await admin.PostAsync($"/api/v1/workitems/{task.Id}/complete", null)).ShouldBeSuccessAsync();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_template_over_the_cap_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var responsibilityId = await AResponsibilityAsync(app, admin);

        var tooMany = Enumerable.Range(0, 51).Select(index => ($"Step {index}", false)).ToArray();

        var refused = await admin.PutJsonAsync(
            $"/api/v1/responsibilities/{responsibilityId}/checklist-template",
            Template(tooMany));

        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
