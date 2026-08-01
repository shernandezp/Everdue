using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Recurrence;
using Everdue.Server.Tests.Support;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Responsibility edits are audited the way work-item edits are. The rules decide what the ledger
/// will ever record — "who changed this weekly rule to yearly, and what did it say before" must be
/// answerable, or the compliance numbers rest on rules nobody can vouch for.
/// </summary>
public class ResponsibilityAuditTests
{
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Every_responsibility_mutation_writes_an_event_with_the_old_values(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var created = await client.PostJsonAsync<ResponsibilityDto>("/api/v1/responsibilities", new
        {
            title = "Weekly follow-up",
            ownerUserId = ownerId,
            recurrenceKind = nameof(RecurrenceKind.WeeklyOnDays),
            daysOfWeekMask = RecurrenceRule.MaskFor(DayOfWeek.Monday),
            startDate = "2026-07-27",
        });

        // The dangerous edit: weekly -> yearly stops nine-tenths of future misses from ever being
        // recorded. The event must carry the rule as it stood before.
        await client.PutJsonAsync<ResponsibilityDto>($"/api/v1/responsibilities/{created.Id}", new
        {
            title = "Weekly follow-up",
            ownerUserId = ownerId,
            recurrenceKind = nameof(RecurrenceKind.Yearly),
            dayOfMonth = 1,
            monthOfYear = 1,
            startDate = "2026-07-27",
            active = true,
        });

        await client.PostJsonAsync<ResponsibilityDto>($"/api/v1/responsibilities/{created.Id}/pause", new { until = "2026-08-15" });
        await client.PostJsonAsync<ResponsibilityDto>($"/api/v1/responsibilities/{created.Id}/resume");
        await client.DeleteFromJsonAsync<ResponsibilityDto>($"/api/v1/responsibilities/{created.Id}");

        var events = await client.GetJsonAsync<IReadOnlyList<ResponsibilityEventDto>>(
            $"/api/v1/responsibilities/{created.Id}/events");

        events.Select(e => e.EventType).ShouldBe(
        [
            ResponsibilityEventType.Created,
            ResponsibilityEventType.Updated,
            ResponsibilityEventType.Paused,
            ResponsibilityEventType.Resumed,
            ResponsibilityEventType.Deactivated,
        ]);

        events.ShouldAllBe(e => e.UserId != Guid.Empty);

        var ruleChange = events.Single(e => e.EventType == ResponsibilityEventType.Updated);
        ruleChange.DataJson.ShouldNotBeNull();
        ruleChange.DataJson.ShouldContain("recurrenceKind");
        ruleChange.DataJson.ShouldContain(nameof(RecurrenceKind.WeeklyOnDays)); // the old value survives
        ruleChange.DataJson.ShouldContain(nameof(RecurrenceKind.Yearly));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_save_that_changed_nothing_writes_no_event(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var created = await client.PostJsonAsync<ResponsibilityDto>("/api/v1/responsibilities", new
        {
            title = "Monthly inventory check",
            ownerUserId = ownerId,
            recurrenceKind = nameof(RecurrenceKind.MonthlyOnDay),
            dayOfMonth = 1,
            startDate = "2026-07-01",
        });

        await client.PutJsonAsync<ResponsibilityDto>($"/api/v1/responsibilities/{created.Id}", new
        {
            title = "Monthly inventory check",
            ownerUserId = ownerId,
            recurrenceKind = nameof(RecurrenceKind.MonthlyOnDay),
            dayOfMonth = 1,
            startDate = "2026-07-01",
            active = true,
        });

        var events = await client.GetJsonAsync<IReadOnlyList<ResponsibilityEventDto>>(
            $"/api/v1/responsibilities/{created.Id}/events");

        events.Select(e => e.EventType).ShouldBe([ResponsibilityEventType.Created]);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_handover_is_typed_as_a_reassignment(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();
        var ownerId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var created = await client.PostJsonAsync<ResponsibilityDto>("/api/v1/responsibilities", new
        {
            title = "Friday vehicle inspection",
            ownerUserId = ownerId,
            recurrenceKind = nameof(RecurrenceKind.WeeklyOnDays),
            daysOfWeekMask = RecurrenceRule.MaskFor(DayOfWeek.Friday),
            startDate = "2026-07-27",
        });

        await client.PostJsonAsync<ReassignResultDto>($"/api/v1/responsibilities/{created.Id}/reassign", new
        {
            newOwnerUserId = memberId,
            applyToWorkableOccurrences = false,
        });

        var events = await client.GetJsonAsync<IReadOnlyList<ResponsibilityEventDto>>(
            $"/api/v1/responsibilities/{created.Id}/events");

        var reassigned = events.Single(e => e.EventType == ResponsibilityEventType.Reassigned);
        reassigned.DataJson.ShouldNotBeNull();
        reassigned.DataJson.ShouldContain(ownerId.ToString());   // from
        reassigned.DataJson.ShouldContain(memberId.ToString()); // to
    }

    public static TheoryData<TestProvider> Providers => TestDatabases.All;
}
