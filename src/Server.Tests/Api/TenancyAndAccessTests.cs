using System.Net;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Infrastructure.Persistence;
using Everdue.Server.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Api;

/// <summary>
/// Acceptance criterion 6, verified rather than trusted: a row belonging to another tenant is
/// invisible to reads, to writes and to transitions.
/// </summary>
public class TenancyIsolationTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_row_belonging_to_another_tenant_is_invisible(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var foreign = await SeedForeignTenantAsync(app);

        var entities = await client.GetJsonAsync<PagedResult<EntityDto>>("/api/v1/entities?includeInactive=true");
        entities.Items.ShouldNotContain(e => e.Id == foreign.EntityId);
        entities.TotalCount.ShouldBe(0);

        var workItems = await client.GetJsonAsync<PagedResult<WorkItemDto>>("/api/v1/workitems?includeCancelled=true");
        workItems.Items.ShouldNotContain(w => w.Id == foreign.WorkItemId);

        var users = await client.GetJsonAsync<IReadOnlyList<UserDto>>("/api/v1/users");
        users.ShouldNotContain(u => u.Id == foreign.UserId);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_foreign_row_cannot_be_read_updated_or_transitioned_by_id(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        var foreign = await SeedForeignTenantAsync(app);

        (await client.GetAsync($"/api/v1/workitems/{foreign.WorkItemId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/v1/entities/{foreign.EntityId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await client.PostJsonAsync($"/api/v1/workitems/{foreign.WorkItemId}/complete")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

        (await client.PutJsonAsync($"/api/v1/entities/{foreign.EntityId}", new { name = "Hijacked", type = nameof(EntityType.Customer), active = true }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Reports_never_count_another_tenants_rows(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = await app.SignInAsAdminAsync();

        await SeedForeignTenantAsync(app);

        var exceptions = await client.GetJsonAsync<ExceptionsReportDto>("/api/v1/reports/exceptions");
        exceptions.DueToday.Count.ShouldBe(0);
        exceptions.Overdue.Count.ShouldBe(0);
        exceptions.OnHold.Count.ShouldBe(0);

        var health = await client.GetJsonAsync<PagedResult<EntityHealthRowDto>>("/api/v1/reports/entity-health");
        health.TotalCount.ShouldBe(0);
    }

    private sealed record ForeignRows(Guid TenantId, Guid UserId, Guid EntityId, Guid WorkItemId);

    /// <summary>
    /// Writes rows for a second tenant directly through the DbContext, with explicit TenantIds, so
    /// nothing about the test relies on the API being willing to create them.
    /// </summary>
    private static async Task<ForeignRows> SeedForeignTenantAsync(EverdueApp app)
        => await app.ScopedAsync(async services =>
        {
            var db = services.GetRequiredService<EverdueDbContext>();

            var tenant = new Tenant
            {
                Id = Guid.CreateVersion7(),
                Name = "Other company",
                TimeZoneId = "UTC",
                DefaultLanguage = Languages.English,
                Active = true,
            };

            var user = new AppUser
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Id,
                UserName = "intruder@other.test",
                NormalizedUserName = "INTRUDER@OTHER.TEST",
                Email = "intruder@other.test",
                NormalizedEmail = "INTRUDER@OTHER.TEST",
                DisplayName = "Intruder",
                Role = UserRole.Admin,
                Active = true,
                SecurityStamp = Guid.CreateVersion7().ToString(),
                CreatedAt = DateTimeOffset.UtcNow,
            };

            var entity = new Entity
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Id,
                Name = "Other tenant's customer",
                Type = EntityType.Customer,
                Active = true,
            };

            var workItem = new WorkItem
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Id,
                Title = "Other tenant's task",
                OwnerUserId = user.Id,
                EntityId = entity.Id,
                DueDate = DateTimeOffset.UtcNow,
                Status = WorkItemStatus.OnHold,
                HoldReason = HoldReason.WaitingCustomer,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            db.Tenants.Add(tenant);
            db.Users.Add(user);
            db.Entities.Add(entity);
            db.WorkItems.Add(workItem);
            await db.SaveChangesAsync();

            // Proof the rows really are in the table: the filter, not an empty database, is what hides them.
            (await db.WorkItems.IgnoreQueryFilters().CountAsync(w => w.TenantId == tenant.Id)).ShouldBe(1);

            return new ForeignRows(tenant.Id, user.Id, entity.Id, workItem.Id);
        });
}

/// <summary>
/// Members run the board; administrators run the instance. v1 keeps reports admin-only — the
/// simplest rule that fits the product, revisited when usage says otherwise.
/// </summary>
public class RoleGatingTests
{
    public static TheoryData<TestProvider> Providers => TestDatabases.All;

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_member_may_run_the_board_but_not_administer(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        // Allowed: read reference data, create and work one-off tasks.
        (await member.GetAsync("/api/v1/entities")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await member.GetAsync("/api/v1/departments")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await member.GetAsync("/api/v1/workitems?view=board")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var task = await member.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Member's own task",
            ownerUserId = memberId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        (await member.PostJsonAsync($"/api/v1/workitems/{task.Id}/complete")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Refused: everything that shapes the instance.
        (await member.PostJsonAsync("/api/v1/entities", new { name = "Nope", type = nameof(EntityType.Customer) }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await member.PostJsonAsync("/api/v1/departments", new { name = "Nope" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await member.GetAsync("/api/v1/reports/exceptions")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.GetAsync("/api/v1/reports/entity-health")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.GetAsync("/api/v1/reports/neglect")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.GetAsync("/api/v1/reports/blocked-by-entity")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await member.PostJsonAsync("/api/v1/users", new
        {
            email = "nope@everdue.test",
            password = "Everdue2026Nope!",
            displayName = "Nope",
            role = nameof(UserRole.Member),
        })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await member.PutJsonAsync("/api/v1/settings/tenant", new
        {
            name = "Hijacked",
            timeZoneId = "UTC",
            digestHourLocal = 7,
            defaultLanguage = "en",
        })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A member has to be able to see who can own a task, or they cannot create work at all — which
    /// is most of what a member does. The directory is therefore readable by anyone signed in, but
    /// narrowed: no deactivated colleagues, and no view of who is mid-password-reset.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_member_can_read_the_user_directory_but_only_the_assignable_part(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var member = await app.SignInAsMemberAsync();

        var retired = await admin.PostJsonAsync<UserDto>("/api/v1/users", new
        {
            email = "retired@everdue.test",
            password = "Everdue2026Retired!",
            displayName = "Retired",
            role = nameof(UserRole.Member),
        });

        await admin.PutJsonAsync<UserDto>($"/api/v1/users/{retired.Id}", new
        {
            displayName = "Retired",
            role = nameof(UserRole.Member),
            active = false,
        });

        var asMember = await member.GetJsonAsync<IReadOnlyList<UserDto>>("/api/v1/users?includeInactive=true");
        asMember.ShouldNotContain(u => u.Id == retired.Id);
        asMember.ShouldContain(u => u.Email == EverdueApp.AdminEmail);
        asMember.ShouldAllBe(u => u.Active && !u.MustChangePassword);

        var asAdmin = await admin.GetJsonAsync<IReadOnlyList<UserDto>>("/api/v1/users?includeInactive=true");
        asAdmin.ShouldContain(u => u.Id == retired.Id);
    }

    /// <summary>The entity drilldown is an entity screen, and members are meant to have those.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_member_can_open_an_entity_timeline(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var member = await app.SignInAsMemberAsync();

        var entity = await admin.PostJsonAsync<EntityDto>("/api/v1/entities", new
        {
            name = "Acme",
            type = nameof(EntityType.Customer),
        });

        var timeline = await member.GetJsonAsync<EntityTimelineDto>($"/api/v1/reports/entities/{entity.Id}/timeline");
        timeline.EntityId.ShouldBe(entity.Id);
    }

    /// <summary>The whole point of the directory being readable: a member assigning work to a colleague.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_member_can_create_a_task_for_a_colleague(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var member = await app.SignInAsMemberAsync();

        var directory = await member.GetJsonAsync<IReadOnlyList<UserDto>>("/api/v1/users");
        var colleague = directory.Single(u => u.Email == EverdueApp.AdminEmail);

        var task = await member.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Handover",
            ownerUserId = colleague.Id,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        task.OwnerUserId.ShouldBe(colleague.Id);
        task.OwnerDisplayName.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Anyone may work and edit anyone's item — in a team this size that is cover, not overreach.
    /// Undoing a completion and cancelling are the two exceptions, because both erase a record.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Working_and_editing_someone_elses_item_is_open_undoing_it_is_not(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var member = await app.SignInAsMemberAsync();

        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Admin's task",
            ownerUserId = adminId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        // Editing a colleague's item, including handing it over, is allowed.
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);
        var edited = await member.PutJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}", new
        {
            title = "Renamed by a colleague",
            ownerUserId = memberId,
        });

        edited.Title.ShouldBe("Renamed by a colleague");
        edited.OwnerUserId.ShouldBe(memberId);

        // Working it is too.
        (await admin.PostJsonAsync($"/api/v1/workitems/{task.Id}/complete")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Undoing the completion of an item you do not own is not.
        (await admin.PutJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}", new
        {
            title = "Renamed by a colleague",
            ownerUserId = memberId,
        })).OwnerUserId.ShouldBe(memberId);

        var otherMember = await app.SignInAsMemberAsync();
        var adminsOwn = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Admin keeps this one",
            ownerUserId = adminId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await otherMember.PostJsonAsync($"/api/v1/workitems/{adminsOwn.Id}/complete");
        (await otherMember.PostJsonAsync($"/api/v1/workitems/{adminsOwn.Id}/reopen")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The price of open editing: every change is attributed, field by field, with the old value.
    ///
    /// An edit that moved the owner is typed <c>Reassigned</c> rather than <c>Updated</c> — same
    /// payload, different type, so "who was handed what" is an indexed query rather than a JSON scan.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Editing_someone_elses_item_is_recorded_with_who_changed_what(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var member = await app.SignInAsMemberAsync();

        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);
        var memberId = await app.UserIdAsync(EverdueApp.MemberEmail);

        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Original title",
            ownerUserId = adminId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await member.PutJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}", new
        {
            title = "Corrected title",
            ownerUserId = memberId,
        });

        var detail = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{task.Id}");

        // The owner moved, so this edit is a hand-over and is typed as one.
        var edit = detail.Events.Single(e => e.EventType == WorkItemEventType.Reassigned);

        edit.UserId.ShouldBe(memberId);
        edit.UserDisplayName.ShouldBe("Member");

        edit.DataJson.ShouldNotBeNull();
        edit.DataJson!.ShouldContain("Original title");   // the old value, not just the new one
        edit.DataJson.ShouldContain("Corrected title");
        edit.DataJson.ShouldContain(adminId.ToString());  // handed over from
        edit.DataJson.ShouldContain(memberId.ToString()); // handed over to
    }

    /// <summary>An edit that leaves the owner alone stays an ordinary Updated event.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_edit_that_does_not_move_the_owner_is_not_a_reassignment(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Original title",
            ownerUserId = adminId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PutJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}", new
        {
            title = "Retitled",
            ownerUserId = adminId,
        });

        var detail = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{task.Id}");

        detail.Events.ShouldContain(e => e.EventType == WorkItemEventType.Updated);
        detail.Events.ShouldNotContain(e => e.EventType == WorkItemEventType.Reassigned);
    }

    /// <summary>A save that changes nothing is not history, and must not clutter the drawer.</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task An_edit_that_changes_nothing_records_nothing(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var admin = await app.SignInAsAdminAsync();
        var adminId = await app.UserIdAsync(EverdueApp.AdminEmail);

        var task = await admin.PostJsonAsync<WorkItemDto>("/api/v1/workitems", new
        {
            title = "Unchanged",
            ownerUserId = adminId,
            dueDate = app.Clock.UtcNow.AddDays(1),
        });

        await admin.PutJsonAsync<WorkItemDto>($"/api/v1/workitems/{task.Id}", new
        {
            title = "Unchanged",
            ownerUserId = adminId,
        });

        var detail = await admin.GetJsonAsync<WorkItemDetailDto>($"/api/v1/workitems/{task.Id}");
        detail.Events.ShouldNotContain(e => e.EventType == WorkItemEventType.Updated);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Anonymous_requests_are_rejected_without_a_redirect(TestProvider provider)
    {
        await using var app = await EverdueApp.StartAsync(provider);
        var client = app.NewClient();

        var response = await client.GetAsync("/api/v1/workitems");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // never a 302 to a login page
    }
}
