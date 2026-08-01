using Everdue.Server.Application.Common;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Recurrence;

namespace Everdue.Server.Infrastructure.Demo;

/// <summary>
/// How a demo responsibility is supposed to have behaved. Percentages of concluded periods; whatever is left
/// over after these three is a plain miss.
/// </summary>
public sealed record DemoBehaviour(int OnTime, int Late, int Blocked)
{
    /// <summary>The ordinary case: mostly done, occasionally late, rarely blocked.</summary>
    public static readonly DemoBehaviour Reliable = new(86, 7, 4);

    public static readonly DemoBehaviour Patchy = new(62, 14, 12);

    /// <summary>Exists so <c>/insights/chronic</c> and the dashboard's chronic card have something to say.</summary>
    public static readonly DemoBehaviour Chronic = new(38, 10, 14);
}

/// <summary>What happened to one concluded period.</summary>
internal enum DemoOutcome
{
    OnTime,
    Late,
    BlockedThenLate,
    BlockedThenMissed,
    Missed,
}

/// <summary>Rows produced for one responsibility, added in one batch.</summary>
public sealed class DemoLedger
{
    public List<WorkItem> Items { get; } = [];

    public List<WorkItemEvent> Events { get; } = [];

    public List<ChecklistItem> Checklist { get; } = [];
}

/// <summary>
/// Writes the demo ledger directly — work items, their status events and their hold intervals — with plausible
/// timestamps over the last few months.
///
/// <para><strong>Why not simply back-date the responsibilities and let the engine catch up:</strong> because
/// that produces a ledger of nothing but misses. The engine inserts an already-concluded period as
/// <c>Missed</c>, correctly — it is the trap the README warns about — and a demo built that way shows every
/// screen at 0% compliance, teaching a stranger the opposite of what the product does.</para>
///
/// <para>Randomness comes from a fixed seed, so two demo installs look the same and a screenshot stays true.</para>
/// </summary>
public sealed class DemoLedgerBuilder(Random random)
{
    /// <summary>
    /// Every period of one responsibility from <paramref name="from"/> up to and including the one currently
    /// open. Concluded periods get an outcome; the open one is left workable, sometimes on hold so that
    /// blocked-by-entity and hold aging have something live in them.
    /// </summary>
    public DemoLedger Build(
        Responsibility responsibility,
        IReadOnlyList<ChecklistTemplateItem> template,
        DemoBehaviour behaviour,
        IReadOnlyList<Guid> candidateOwners,
        TimeZoneInfo timeZone,
        DateOnly from,
        DateTimeOffset now)
    {
        var rule = responsibility.ToRule();
        var ledger = new DemoLedger();

        var cursor = RecurrenceCalculator.FirstScheduledOnOrAfter(rule, from);
        var guard = 0;

        while (guard++ < 3000)
        {
            var period = OccurrencePeriod.For(rule, cursor, timeZone);

            if (period.PeriodStart > now)
            {
                break;
            }

            var item = new WorkItem
            {
                Id = Guid.CreateVersion7(),
                TenantId = responsibility.TenantId,
                ResponsibilityId = responsibility.Id,
                Title = responsibility.Title,
                Description = responsibility.Description,
                OwnerUserId = candidateOwners[random.Next(candidateOwners.Count)],
                EntityId = responsibility.EntityId,
                DepartmentId = responsibility.DepartmentId,
                PeriodStart = period.PeriodStart,
                PeriodEnd = period.PeriodEnd,
                DueDate = period.DueDate,
                CreatedAt = period.PeriodStart,
                Status = WorkItemStatus.Open,
            };

            ledger.Items.Add(item);

            ledger.Events.Add(WorkItemEventFactory.Created(item, null, period.PeriodStart, new
            {
                source = WorkItemSources.Engine,
                responsibilityId = responsibility.Id,
                scheduledFor = period.PeriodStart,
                catchUp = false,
                demo = true,
            }));

            foreach (var line in template)
            {
                var copy = ChecklistItem.FromTemplate(line, item);
                ledger.Checklist.Add(copy);
            }

            if (period.PeriodEnd <= now)
            {
                Conclude(ledger, item, period, behaviour);
                TickChecklist(ledger, item);
            }
            else
            {
                LeaveOpen(ledger, item, period, now);
            }

            cursor = RecurrenceCalculator.NextScheduledDate(rule, cursor);
        }

        return ledger;
    }

    private void Conclude(DemoLedger ledger, WorkItem item, OccurrencePeriod period, DemoBehaviour behaviour)
    {
        switch (Roll(behaviour))
        {
            case DemoOutcome.OnTime:
                Complete(ledger, item, Between(period.PeriodStart, period.DueDate), WorkItemStatus.Completed);
                break;

            case DemoOutcome.Late:
                Miss(ledger, item, period, WorkItemStatus.Open);
                Complete(ledger, item, period.PeriodEnd.AddDays(random.Next(0, 4)).AddHours(random.Next(1, 20)), WorkItemStatus.CompletedLate);
                break;

            case DemoOutcome.BlockedThenLate:
                Hold(ledger, item, period, out var releasedAt);
                Release(ledger, item, releasedAt);
                Miss(ledger, item, period, WorkItemStatus.Open);
                Complete(ledger, item, period.PeriodEnd.AddDays(random.Next(0, 3)).AddHours(random.Next(1, 18)), WorkItemStatus.CompletedLate);
                break;

            case DemoOutcome.BlockedThenMissed:
                // The hold was still on when the period closed, so the engine's miss is what ended it. That is a
                // normal exit from a hold, and hold aging counts it as one.
                Hold(ledger, item, period, out _);
                Miss(ledger, item, period, WorkItemStatus.OnHold);
                break;

            default:
                Miss(ledger, item, period, WorkItemStatus.Open);
                break;
        }
    }

    /// <summary>
    /// The period still running. Left workable, and one in four is parked on hold — the exception dashboard,
    /// blocked-by-entity and hold aging's "still on hold" all need something live to show.
    /// </summary>
    private void LeaveOpen(DemoLedger ledger, WorkItem item, OccurrencePeriod period, DateTimeOffset now)
    {
        var roll = random.Next(100);

        if (roll < 25)
        {
            var reason = (HoldReason)random.Next(0, 4);
            var heldAt = Between(period.PeriodStart, now);

            item.Status = WorkItemStatus.OnHold;
            item.HoldReason = reason;

            ledger.Events.Add(WorkItemEventFactory.StatusChanged(
                item,
                item.OwnerUserId,
                heldAt,
                WorkItemStatus.Open,
                WorkItemStatus.OnHold,
                new { reason = reason.ToString(), text = (string?)null }));

            return;
        }

        if (roll < 45)
        {
            item.Status = WorkItemStatus.InProgress;

            ledger.Events.Add(WorkItemEventFactory.StatusChanged(
                item,
                item.OwnerUserId,
                Between(period.PeriodStart, now),
                WorkItemStatus.Open,
                WorkItemStatus.InProgress,
                null));
        }
    }

    /// <summary>
    /// A completed occurrence's checklist is ticked; a missed one's is partly ticked, which is what a real
    /// half-done inspection looks like.
    /// </summary>
    private void TickChecklist(DemoLedger ledger, WorkItem item)
    {
        var lines = ledger.Checklist.Where(c => c.WorkItemId == item.Id).ToList();

        if (lines.Count == 0)
        {
            return;
        }

        var completed = item.Status.IsCompletion();
        var at = item.CompletedAt ?? item.DueDate;

        foreach (var line in lines)
        {
            if (completed || random.Next(100) < 40)
            {
                line.CheckedAt = at;
                line.CheckedByUserId = item.OwnerUserId;
            }
        }
    }

    private DemoOutcome Roll(DemoBehaviour behaviour)
    {
        var roll = random.Next(100);

        if (roll < behaviour.OnTime)
        {
            return DemoOutcome.OnTime;
        }

        if (roll < behaviour.OnTime + behaviour.Late)
        {
            return DemoOutcome.Late;
        }

        if (roll < behaviour.OnTime + behaviour.Late + behaviour.Blocked)
        {
            return random.Next(2) == 0 ? DemoOutcome.BlockedThenLate : DemoOutcome.BlockedThenMissed;
        }

        return DemoOutcome.Missed;
    }

    private void Hold(DemoLedger ledger, WorkItem item, OccurrencePeriod period, out DateTimeOffset releasedAt)
    {
        var reason = (HoldReason)random.Next(0, 4);
        var heldAt = Between(period.PeriodStart, period.DueDate);

        item.Status = WorkItemStatus.OnHold;
        item.HoldReason = reason;

        ledger.Events.Add(WorkItemEventFactory.StatusChanged(
            item,
            item.OwnerUserId,
            heldAt,
            WorkItemStatus.Open,
            WorkItemStatus.OnHold,
            new { reason = reason.ToString(), text = (string?)null }));

        releasedAt = heldAt.AddDays(random.Next(1, 8)).AddHours(random.Next(0, 8));
    }

    private static void Release(DemoLedger ledger, WorkItem item, DateTimeOffset at)
    {
        item.Status = WorkItemStatus.Open;
        item.ClearHold();

        ledger.Events.Add(WorkItemEventFactory.StatusChanged(item, item.OwnerUserId, at, WorkItemStatus.OnHold, WorkItemStatus.Open, null));
    }

    private static void Miss(DemoLedger ledger, WorkItem item, OccurrencePeriod period, WorkItemStatus from)
    {
        item.Status = WorkItemStatus.Missed;

        // The engine is the only actor that records a miss, so the event carries no user — exactly as a real one
        // does, which is what makes the demo's insight numbers real numbers.
        ledger.Events.Add(WorkItemEventFactory.StatusChanged(item, null, period.PeriodEnd, from, WorkItemStatus.Missed, new
        {
            priorStatus = from.ToString(),
            holdReason = item.HoldReason?.ToString(),
            periodEnd = period.PeriodEnd,
            demo = true,
        }));
    }

    private void Complete(DemoLedger ledger, WorkItem item, DateTimeOffset at, WorkItemStatus status)
    {
        var from = item.Status;

        item.Status = status;
        item.CompletedAt = at;
        item.CompletedByUserId = item.OwnerUserId;
        item.ClearHold();

        ledger.Events.Add(WorkItemEventFactory.StatusChanged(item, item.OwnerUserId, at, from, status, null));
    }

    private DateTimeOffset Between(DateTimeOffset from, DateTimeOffset to)
    {
        var span = to - from;

        if (span <= TimeSpan.Zero)
        {
            return from;
        }

        return from + TimeSpan.FromMinutes(random.Next(1, (int)Math.Max(2, span.TotalMinutes)));
    }
}
