using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Proof of completion: the rule over attachments that makes the inspection use case real rather than
/// advisory.
/// </summary>
public class CompletionProofTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static MultipartFormDataContent APhoto()
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes("not really a jpeg"));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        return new MultipartFormDataContent { { content, "file", "line-2.jpg" } };
    }

    private static async Task<Guid> AnOccurrenceRequiringProofAsync(EverdueApp app, HttpClient admin)
    {
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var responsibility = await admin.PostJsonAsync<ResponsibilityDto>("/api/v1/responsibilities", new
        {
            title = "Forklift safety inspection",
            ownerUserId = ownerId,
            recurrenceKind = nameof(RecurrenceKind.Daily),
            startDate = DateOnly.FromDateTime(app.Clock.UtcNow.UtcDateTime),
            requireAttachmentToComplete = true,
        });

        await app.TickEngineAsync();

        return await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            return await db.WorkItems.Where(w => w.ResponsibilityId == responsibility.Id).Select(w => w.Id).FirstAsync();
        });
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Completion_is_refused_without_an_attachment_and_allowed_with_one(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var occurrenceId = await AnOccurrenceRequiringProofAsync(app, admin);

        var detail = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{occurrenceId}");
        detail.CompletionRequirements!.AttachmentRequired.ShouldBeTrue();
        detail.CompletionRequirements.AttachmentCount.ShouldBe(0);

        var blocked = await admin.PostAsync($"/api/v1/workitems/{occurrenceId}/complete", null);
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await blocked.Content.ReadAsStringAsync()).ShouldContain("photo or file");

        await (await admin.PostAsync($"/api/v1/workitems/{occurrenceId}/attachments", APhoto())).ShouldBeSuccessAsync();

        await (await admin.PostAsync($"/api/v1/workitems/{occurrenceId}/complete", null)).ShouldBeSuccessAsync();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Deleting_the_attachment_afterwards_does_not_reopen_the_item(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        var occurrenceId = await AnOccurrenceRequiringProofAsync(app, admin);

        await admin.PostAsync($"/api/v1/workitems/{occurrenceId}/attachments", APhoto());
        await admin.PostAsync($"/api/v1/workitems/{occurrenceId}/complete", null);

        var attachments = await admin.GetJsonAsync<IReadOnlyList<AttachmentDto>>($"/api/v1/workitems/{occurrenceId}/attachments");
        await (await admin.DeleteAsync($"/api/v1/attachments/{attachments.Single().Id}")).ShouldBeSuccessAsync();

        // The rule gates the *transition*, not the state. A completion already recorded stays recorded — the
        // alternative would let deleting a file rewrite the ledger.
        var after = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{occurrenceId}");
        after.Item.Status.ShouldBe(WorkItemStatus.Completed);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Switching_the_rule_on_afterwards_does_not_touch_what_is_already_completed(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var responsibility = await admin.PostJsonAsync<ResponsibilityDto>("/api/v1/responsibilities", new
        {
            title = "Cold room log",
            ownerUserId = ownerId,
            recurrenceKind = nameof(RecurrenceKind.Daily),
            startDate = DateOnly.FromDateTime(app.Clock.UtcNow.UtcDateTime),
        });

        await app.TickEngineAsync();

        var occurrenceId = await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            return await db.WorkItems.Where(w => w.ResponsibilityId == responsibility.Id).Select(w => w.Id).FirstAsync();
        });

        await (await admin.PostAsync($"/api/v1/workitems/{occurrenceId}/complete", null)).ShouldBeSuccessAsync();

        // Now the rule arrives.
        await admin.PutJsonAsync<ResponsibilityDto>($"/api/v1/responsibilities/{responsibility.Id}", new
        {
            title = responsibility.Title,
            ownerUserId = ownerId,
            recurrenceKind = nameof(RecurrenceKind.Daily),
            startDate = responsibility.StartDate,
            active = true,
            requireAttachmentToComplete = true,
        });

        var after = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{occurrenceId}");
        after.Item.Status.ShouldBe(WorkItemStatus.Completed);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_bulk_complete_reports_the_blocked_ones_and_completes_the_rest(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var gated = await AnOccurrenceRequiringProofAsync(app, admin);

        var free = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Nothing required here",
            ownerUserId = ownerId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        // Bulk dispatches the same single-item command, so the gate applies with no special-casing at all.
        var result = await admin.PostJsonAsync<BulkResultDto>("/api/v1/workitems/bulk", new
        {
            ids = new[] { gated, free.Id },
            action = "Complete",
        });

        result.Succeeded.ShouldBe([free.Id]);
        result.Failed.Single().Id.ShouldBe(gated);
        result.Failed.Single().Error.ShouldContain("photo or file");
    }
}
