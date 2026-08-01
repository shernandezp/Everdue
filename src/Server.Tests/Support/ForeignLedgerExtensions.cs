using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Identity;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everdue.Server.Tests.Support;

public static class ForeignLedgerExtensions
{
    /// <summary>
    /// A second tenant with a full recurring history — a responsibility, concluded occurrences, a
    /// completion and a hold with its events — written straight through the DbContext with explicit
    /// tenant ids.
    ///
    /// The insight reports read occurrences, completions and hold events, so a cross-tenant test needs
    /// all three to exist; and the assertion at the end proves the rows really are in the tables, so a
    /// passing test means the filter hid them rather than that nothing was ever written.
    /// </summary>
    public static Task SeedForeignLedgerAsync(this EverdueApp app) => app.ScopedAsync(async services =>
    {
        var db = services.GetRequiredService<EverdueDbContext>();
        var now = app.Clock.UtcNow;

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
            CreatedAt = now,
        };

        var entity = new Entity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Id,
            Name = "Other tenant's customer",
            Type = EntityType.Customer,
            Active = true,
        };

        var responsibility = new Responsibility
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Id,
            Title = "Other tenant's daily duty",
            OwnerUserId = user.Id,
            EntityId = entity.Id,
            RecurrenceKind = RecurrenceKind.Daily,
            StartDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-30),
            Active = true,
        };

        db.Tenants.Add(tenant);
        db.Users.Add(user);
        db.Entities.Add(entity);
        db.Responsibilities.Add(responsibility);

        var occurrences = new List<WorkItem>();

        for (var day = 1; day <= 10; day++)
        {
            var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(-day);

            occurrences.Add(new WorkItem
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Id,
                ResponsibilityId = responsibility.Id,
                Title = responsibility.Title,
                OwnerUserId = user.Id,
                EntityId = entity.Id,
                PeriodStart = start,
                PeriodEnd = start.AddDays(1),
                DueDate = start.AddDays(1).AddSeconds(-1),
                Status = day <= 5 ? WorkItemStatus.Missed : WorkItemStatus.Completed,
                CompletedAt = day <= 5 ? null : start.AddHours(9),
                CompletedByUserId = day <= 5 ? null : user.Id,
                CreatedAt = start,
            });
        }

        var held = occurrences[0];
        held.HoldReason = HoldReason.WaitingCustomer;

        db.WorkItems.AddRange(occurrences);

        db.WorkItemEvents.Add(new WorkItemEvent
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Id,
            WorkItemId = held.Id,
            UserId = user.Id,
            Timestamp = held.PeriodStart!.Value.AddHours(2),
            EventType = WorkItemEventType.StatusChanged,
            FromStatus = WorkItemStatus.Open,
            ToStatus = WorkItemStatus.OnHold,
            DataJson = "{\"reason\":\"WaitingCustomer\",\"text\":null}",
        });

        db.WorkItemEvents.Add(new WorkItemEvent
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Id,
            WorkItemId = held.Id,
            UserId = user.Id,
            Timestamp = held.PeriodStart!.Value.AddHours(6),
            EventType = WorkItemEventType.Reassigned,
            DataJson = "{\"changes\":[{\"field\":\"ownerUserId\",\"from\":null,\"to\":null}]}",
        });

        await db.SaveChangesAsync();

        (await db.WorkItems.IgnoreQueryFilters().CountAsync(w => w.TenantId == tenant.Id)).ShouldBe(10);
        (await db.WorkItemEvents.IgnoreQueryFilters().CountAsync(e => e.TenantId == tenant.Id)).ShouldBe(2);
    });
}
