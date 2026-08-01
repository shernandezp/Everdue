using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;

namespace Everdue.Server.Tests.Support;

/// <summary>
/// Builds occurrence history by hand, the way the insight reports read it.
///
/// Rows go in through the DbContext rather than through the API and the engine, so a fixture states
/// exactly the history it means — "thirty weekly periods, twenty-six of them on time" — instead of
/// depending on a scheduler to reproduce it.
/// </summary>
public sealed class LedgerBuilder(EverdueDbContext db, TimeZoneInfo timeZone, DateTimeOffset now)
{
    public DateOnly Today { get; } = TenantTime.LocalDate(now, timeZone);

    public TimeZoneInfo TimeZone => timeZone;

    /// <summary>A tenant-local hour on a local date, as the UTC instant it denotes.</summary>
    public DateTimeOffset At(DateOnly date, int hourLocal = 0) => TenantTime.AtHour(date, hourLocal, timeZone);

    public Entity Entity(string name, EntityType type = EntityType.Customer)
    {
        var entity = new Entity { Id = Guid.CreateVersion7(), Name = name, Type = type, Active = true };
        db.Entities.Add(entity);
        return entity;
    }

    public Responsibility Responsibility(
        string title,
        Guid ownerId,
        Guid? entityId = null,
        bool active = true,
        RecurrenceKind kind = RecurrenceKind.Daily,
        int? daysOfWeekMask = null)
    {
        var responsibility = new Responsibility
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            OwnerUserId = ownerId,
            EntityId = entityId,
            Active = active,
            RecurrenceKind = kind,
            DaysOfWeekMask = daysOfWeekMask,
            StartDate = Today.AddYears(-1),
        };

        db.Responsibilities.Add(responsibility);
        return responsibility;
    }

    /// <summary>
    /// One occurrence covering the local days <c>[periodStart, periodStart + periodDays)</c> — period
    /// start at local midnight, due date at local 23:59:59 of the first day, exactly as the engine
    /// anchors them.
    /// </summary>
    public WorkItem Occurrence(
        Responsibility responsibility,
        DateOnly periodStart,
        int periodDays,
        WorkItemStatus status,
        DateTimeOffset? completedAt = null,
        HoldReason? holdReason = null,
        Guid? ownerId = null)
    {
        var item = new WorkItem
        {
            Id = Guid.CreateVersion7(),
            ResponsibilityId = responsibility.Id,
            Title = responsibility.Title,
            OwnerUserId = ownerId ?? responsibility.OwnerUserId,
            EntityId = responsibility.EntityId,
            DepartmentId = responsibility.DepartmentId,
            PeriodStart = TenantTime.StartOfDay(periodStart, timeZone),
            PeriodEnd = TenantTime.StartOfDay(periodStart.AddDays(periodDays), timeZone),
            DueDate = TenantTime.EndOfDay(periodStart, timeZone),
            Status = status,
            HoldReason = holdReason,
            CreatedAt = TenantTime.StartOfDay(periodStart, timeZone),
            CompletedAt = completedAt ?? (status.IsCompletion() ? TenantTime.EndOfDay(periodStart, timeZone) : null),
            CompletedByUserId = status.IsCompletion() ? ownerId ?? responsibility.OwnerUserId : null,
        };

        db.WorkItems.Add(item);
        return item;
    }

    /// <summary>
    /// <paramref name="count"/> back-to-back periods, the newest of which has already concluded. The
    /// index handed to <paramref name="statusFor"/> counts backwards: 0 is the newest.
    /// </summary>
    public IReadOnlyList<WorkItem> History(
        Responsibility responsibility,
        int count,
        int periodDays,
        Func<int, WorkItemStatus> statusFor,
        DateOnly? lastPeriodStart = null,
        Guid? ownerId = null)
    {
        var start = lastPeriodStart ?? Today.AddDays(-periodDays);
        var items = new List<WorkItem>();

        for (var index = 0; index < count; index++)
        {
            items.Add(Occurrence(
                responsibility,
                start.AddDays(-index * periodDays),
                periodDays,
                statusFor(index),
                ownerId: ownerId));
        }

        return items;
    }

    public WorkItem OneOff(
        string title,
        Guid ownerId,
        WorkItemStatus status,
        DateTimeOffset due,
        DateTimeOffset? completedAt = null,
        Guid? entityId = null)
    {
        var item = new WorkItem
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            OwnerUserId = ownerId,
            EntityId = entityId,
            DueDate = due,
            Status = status,
            CreatedAt = due.AddDays(-1),
            CompletedAt = completedAt,
            CompletedByUserId = completedAt is null ? null : ownerId,
        };

        db.WorkItems.Add(item);
        return item;
    }

    /// <summary>
    /// A stretch of hold time, written exactly as the API writes it: the entry carries the reason in its
    /// payload, and the exit is a status change away from OnHold. Pass no <paramref name="until"/> for a
    /// hold that is still running.
    /// </summary>
    public void Hold(
        WorkItem item,
        HoldReason reason,
        DateTimeOffset from,
        DateTimeOffset? until = null,
        string? entryPayload = null,
        WorkItemStatus exitStatus = WorkItemStatus.Open)
    {
        db.WorkItemEvents.Add(new WorkItemEvent
        {
            Id = Guid.CreateVersion7(),
            WorkItemId = item.Id,
            UserId = item.OwnerUserId,
            Timestamp = from,
            EventType = WorkItemEventType.StatusChanged,
            FromStatus = WorkItemStatus.Open,
            ToStatus = WorkItemStatus.OnHold,
            DataJson = entryPayload ?? $"{{\"reason\":\"{reason}\",\"text\":null}}",
        });

        if (until is { } exit)
        {
            db.WorkItemEvents.Add(new WorkItemEvent
            {
                Id = Guid.CreateVersion7(),
                WorkItemId = item.Id,

                // A miss is written by the engine, which has no user id — the way the real one is.
                UserId = exitStatus == WorkItemStatus.Missed ? null : item.OwnerUserId,
                Timestamp = exit,
                EventType = WorkItemEventType.StatusChanged,
                FromStatus = WorkItemStatus.OnHold,
                ToStatus = exitStatus,
                DataJson = exitStatus == WorkItemStatus.Missed ? "{\"priorStatus\":\"OnHold\"}" : null,
            });
        }
    }

    /// <summary>
    /// A reschedule, which copies the item's current status into both ends of the event. On a held item
    /// that reads as leaving and re-entering the hold in the same instant unless the reader filters on
    /// the event type — which is what this exists to prove it does.
    /// </summary>
    public void Rescheduled(WorkItem item, DateTimeOffset at, WorkItemStatus status = WorkItemStatus.OnHold)
    {
        db.WorkItemEvents.Add(new WorkItemEvent
        {
            Id = Guid.CreateVersion7(),
            WorkItemId = item.Id,
            UserId = item.OwnerUserId,
            Timestamp = at,
            EventType = WorkItemEventType.Rescheduled,
            FromStatus = status,
            ToStatus = status,
            DataJson = "{\"from\":null,\"to\":null,\"note\":null}",
        });
    }

    public void Reassigned(WorkItem item, DateTimeOffset at)
    {
        db.WorkItemEvents.Add(new WorkItemEvent
        {
            Id = Guid.CreateVersion7(),
            WorkItemId = item.Id,
            UserId = item.OwnerUserId,
            Timestamp = at,
            EventType = WorkItemEventType.Reassigned,
            DataJson = "{\"changes\":[{\"field\":\"ownerUserId\",\"from\":null,\"to\":null}]}",
        });
    }

    public Task SaveAsync() => db.SaveChangesAsync();
}
