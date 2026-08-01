using System.Net;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Engine.Digest;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>A view restores exactly what it was saved with, and is shareable.</summary>
public class SavedViewTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_saved_view_round_trips_its_filter_set(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();

        const string query = "status=Missed,OnHold&overdue=true&search=inventory";

        var saved = await member.PostJsonAsync<SavedViewDto>("/api/v1/saved-views", new
        {
            name = "My overdue",
            route = "work",
            queryString = $"?{query}",
        });

        // The leading '?' is not part of the filter set.
        saved.QueryString.ShouldBe(query);

        var listed = await member.GetJsonAsync<IReadOnlyList<SavedViewDto>>("/api/v1/saved-views");
        listed.Single().QueryString.ShouldBe(query);

        // Saving the same name again replaces rather than duplicates.
        await member.PostJsonAsync<SavedViewDto>("/api/v1/saved-views", new
        {
            name = "My overdue",
            route = "work",
            queryString = "status=Open",
        });

        var afterUpdate = await member.GetJsonAsync<IReadOnlyList<SavedViewDto>>("/api/v1/saved-views");
        afterUpdate.Count.ShouldBe(1);
        afterUpdate.Single().QueryString.ShouldBe("status=Open");

        await member.DeleteAsync($"/api/v1/saved-views/{saved.Id}");
        (await member.GetJsonAsync<IReadOnlyList<SavedViewDto>>("/api/v1/saved-views")).ShouldBeEmpty();
    }

    /// <summary>Personal only: one person's views are not another's, even to an administrator.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Saved_views_are_personal(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();
        var admin = await app.SignInAsAdminAsync();

        await member.PostJsonAsync<SavedViewDto>("/api/v1/saved-views", new
        {
            name = "Mine",
            route = "work",
            queryString = "status=Open",
        });

        (await admin.GetJsonAsync<IReadOnlyList<SavedViewDto>>("/api/v1/saved-views")).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_unknown_route_is_refused(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();

        var response = await member.PostJsonAsync("/api/v1/saved-views", new
        {
            name = "Nope",
            route = "reports",
            queryString = "",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}

/// <summary>
/// An upgraded install keeps its daily digest with no admin action, and a
/// weekly subscription only fires on its day.
/// </summary>
public class DigestSubscriptionTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    private static async Task<IReadOnlyList<DueSubscriber>> DueOnAsync(EverdueApp app, DateOnly date)
        => await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();
            var selector = new DigestSubscriptionSelector(db, new UserDirectory(db));
            return await selector.SelectDueAsync(date, CancellationToken.None);
        });

    /// <summary>
    /// The upgrade story with no data migration: an administrator with no row is an implicit daily
    /// subscriber, matching the original default from before subscription rows existed. A member
    /// with no row is not — that is the line between opt-out and opt-in.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Administrators_without_a_subscription_are_implicit_daily_subscribers(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);

        var due = await DueOnAsync(app, DateOnly.Parse("2026-07-29"));

        due.Select(d => d.User.Email).ShouldBe([EverdueApp.AdminEmail]);
        due.Single().Frequency.ShouldBe(DigestFrequency.Daily);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_weekly_subscription_is_due_only_on_its_day(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await admin.PutJsonAsync<DigestSubscriptionDto>("/api/v1/digest-subscriptions", new
        {
            frequency = "Weekly",
            weeklyDayOfWeek = "Monday",
            departmentId = (Guid?)null,
            active = true,
        });

        (await DueOnAsync(app, DateOnly.Parse("2026-07-29"))).ShouldBeEmpty();      // a Wednesday
        (await DueOnAsync(app, DateOnly.Parse("2026-08-03"))).Count.ShouldBe(1);    // the Monday
    }

    /// <summary>Turning the digest off is a row that says so, not the absence of one.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_inactive_subscription_stops_the_digest(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();

        await admin.PutJsonAsync<DigestSubscriptionDto>("/api/v1/digest-subscriptions", new
        {
            frequency = "Daily",
            weeklyDayOfWeek = "Monday",
            departmentId = (Guid?)null,
            active = false,
        });

        (await DueOnAsync(app, DateOnly.Parse("2026-07-29"))).ShouldBeEmpty();
    }

    /// <summary>A member who wants the digest asks for it, and then gets it.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_member_can_subscribe(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();

        await member.PutJsonAsync<DigestSubscriptionDto>("/api/v1/digest-subscriptions", new
        {
            frequency = "Daily",
            weeklyDayOfWeek = "Monday",
            departmentId = (Guid?)null,
            active = true,
        });

        var due = await DueOnAsync(app, DateOnly.Parse("2026-07-29"));
        due.Select(d => d.User.Email).ShouldContain(EverdueApp.MemberEmail);
    }

    /// <summary>The department filter narrows every section, so a manager gets their team's digest.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_department_filter_narrows_the_digest(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var warehouse = await admin.PostJsonAsync<DepartmentDto>("/api/v1/departments", new { name = "Warehouse" });
        var todayEnd = app.Clock.UtcNow.Date.AddDays(1).AddSeconds(-1);

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Warehouse task",
            ownerUserId = adminId,
            departmentId = warehouse.Id,
            dueDate = todayEnd,
        });

        await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Office task",
            ownerUserId = adminId,
            dueDate = todayEnd,
        });

        var scoped = await BuildAsync(app, warehouse.Id);
        var everything = await BuildAsync(app, null);

        scoped.DueToday.Select(i => i.Title).ShouldBe(["Warehouse task"]);
        scoped.DepartmentName.ShouldBe("Warehouse");

        everything.DueToday.Select(i => i.Title).ShouldBe(["Warehouse task", "Office task"], ignoreOrder: true);
    }

    private static Task<DigestContent> BuildAsync(EverdueApp app, Guid? departmentId) => app.ScopedAsync(async services =>
    {
        var db = services.GetRequiredService<EverdueDbContext>();
        var tenant = await db.Tenants.SingleAsync();

        return await new DigestBuilder(db, new UserDirectory(db), services.GetRequiredService<Common.Mediator.ISender>())
            .BuildAsync(tenant, app.Clock.UtcNow, DigestFrequency.Daily, departmentId, CancellationToken.None);
    });
}
