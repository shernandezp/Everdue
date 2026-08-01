using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Notifications;
using Everdue.Server.Application.WorkItems;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Recurrence;
using Everdue.Server.Infrastructure.Options;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Engine;

public sealed record EngineTickResult(int Created, int MarkedMissed, int Skipped)
{
    public static readonly EngineTickResult Empty = new(0, 0, 0);

    public bool DidAnything => Created > 0 || MarkedMissed > 0;
}

/// <summary>
/// The sacred core. Stateless by design: there is no "last run" marker anywhere, so every tick
/// derives the entire ledger from the data. Catch-up after two weeks of downtime and idempotency
/// under a double tick are therefore the same code path, not two features.
/// </summary>
public sealed class OccurrenceEngine(
    EverdueDbContext db,
    ITenantProvider tenants,
    IClock clock,
    IOptions<EngineOptions> options,
    INotificationEnqueuer notifications,
    IWebhookPublisher webhooks,
    IOptions<NotificationOptions> notificationOptions,
    ILogger<OccurrenceEngine> logger)
{
    public async Task<EngineTickResult> TickAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var timeZone = await tenants.GetTimeZoneAsync(cancellationToken);

        var created = 0;
        var skipped = 0;

        var responsibilities = await db.Responsibilities.AsNoTracking()
            .Where(r => r.Active)
            .ToListAsync(cancellationToken);

        // Every template in one query, keyed by responsibility. A per-responsibility read here would
        // turn the tick's flat query count into one-plus-N over the whole schedule.
        var templates = await LoadTemplatesAsync(responsibilities.Select(r => r.Id).ToArray(), cancellationToken);

        var spawned = new List<WorkItem>();

        foreach (var responsibility in responsibilities)
        {
            if (responsibility.IsPausedAt(now))
            {
                skipped++;
                continue;
            }

            templates.TryGetValue(responsibility.Id, out var template);

            var outcome = await GenerateForAsync(responsibility, template ?? [], timeZone, now, spawned, cancellationToken);
            created += outcome.Created;
            skipped += outcome.Skipped;
        }

        var missed = await FlipExpiredToMissedAsync(now, cancellationToken);

        // Notifications and webhooks are enqueued only after the ledger is safely written, in a save of
        // their own. Nothing about telling somebody — or telling another system — may ever be able to
        // take the miss down with it.
        await AnnounceMissesAsync(now, spawned, cancellationToken);

        var result = new EngineTickResult(created, missed, skipped);

        if (result.DidAnything)
        {
            logger.LogInformation(
                "Occurrence tick: {Created} created, {Missed} marked missed, {Skipped} skipped.",
                result.Created,
                result.MarkedMissed,
                result.Skipped);
        }

        return result;
    }

    /// <summary>
    /// Walks the recurrence forward from the latest existing occurrence and inserts everything whose
    /// period has already started. A period that already ended is inserted directly as Missed — two
    /// weeks of downtime produce two weeks of Missed rows, with no gap and no stalled series.
    /// </summary>
    private async Task<Dictionary<Guid, List<ChecklistTemplateItem>>> LoadTemplatesAsync(
        Guid[] responsibilityIds,
        CancellationToken cancellationToken)
    {
        if (responsibilityIds.Length == 0)
        {
            return [];
        }

        var rows = await db.ChecklistTemplateItems.AsNoTracking()
            .Where(t => responsibilityIds.Contains(t.ResponsibilityId))
            .OrderBy(t => t.Position)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(t => t.ResponsibilityId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private async Task<EngineTickResult> GenerateForAsync(
        Responsibility responsibility,
        List<ChecklistTemplateItem> template,
        TimeZoneInfo timeZone,
        DateTimeOffset now,
        List<WorkItem> spawned,
        CancellationToken cancellationToken)
    {
        var rule = responsibility.ToRule();

        if (rule.Validate() is { } problem)
        {
            logger.LogWarning(
                "Responsibility {Id} ('{Title}') has an unusable recurrence and was skipped: {Problem}",
                responsibility.Id,
                responsibility.Title,
                problem);
            return EngineTickResult.Empty;
        }

        var latestPeriod = await db.WorkItems.AsNoTracking()
            .Where(w => w.ResponsibilityId == responsibility.Id)
            .OrderByDescending(w => w.PeriodStart)
            .Select(w => new { w.PeriodStart, w.PeriodEnd })
            .FirstOrDefaultAsync(cancellationToken);

        var cursor = latestPeriod?.PeriodStart is { } latest
            ? RecurrenceCalculator.NextScheduledDate(rule, TenantTime.LocalDate(latest, timeZone))
            : RecurrenceCalculator.FirstScheduledDate(rule);

        // How far the ledger already reaches. Read alongside the cursor because the two can disagree
        // after the tenant's timezone changes: the stored instants keep their old-zone boundaries,
        // and re-reading them in the new zone can shift the cursor's civil date by one.
        var coveredThrough = latestPeriod?.PeriodEnd;

        var cap = options.Value.MaxOccurrencesPerResponsibilityPerTick;
        var created = 0;
        var skipped = 0;
        var pending = new List<WorkItem>();

        for (var guard = 0; guard < cap; guard++)
        {
            var period = OccurrencePeriod.For(rule, cursor, timeZone);

            if (period.PeriodStart > now)
            {
                break;
            }

            // A candidate whose period is mostly behind the ledger's reach is the same civil day
            // spawned twice — the westward-timezone-change case the unique index cannot catch,
            // because the old and new instants differ. A period merely *touching* covered time is
            // kept: an eastward change legitimately overlaps the boundary day by a few hours, and
            // dropping it would silently skip a scheduled date instead.
            if (coveredThrough is { } covered)
            {
                var midpoint = period.PeriodStart + (period.PeriodEnd - period.PeriodStart) / 2;
                if (midpoint < covered)
                {
                    cursor = RecurrenceCalculator.NextScheduledDate(rule, cursor);
                    continue;
                }
            }

            // A period that fell wholly inside a pause window is skipped, never missed — the pause
            // was sanctioned. (Resume ends the window rather than erasing it, precisely so this
            // check still has something to read.)
            if (responsibility.PausedUntil is { } pausedUntil && period.PeriodEnd <= pausedUntil)
            {
                skipped++;
                cursor = RecurrenceCalculator.NextScheduledDate(rule, cursor);
                continue;
            }

            var item = new WorkItem
            {
                Id = Guid.CreateVersion7(),
                TenantId = responsibility.TenantId,
                ResponsibilityId = responsibility.Id,
                Title = responsibility.Title,
                Description = responsibility.Description,
                OwnerUserId = responsibility.OwnerUserId,
                EntityId = responsibility.EntityId,
                DepartmentId = responsibility.DepartmentId,
                PeriodStart = period.PeriodStart,
                PeriodEnd = period.PeriodEnd,
                DueDate = period.DueDate,
                Status = period.PeriodEnd <= now ? WorkItemStatus.Missed : WorkItemStatus.Open,
                CreatedAt = now,
            };

            pending.Add(item);
            created++;

            cursor = RecurrenceCalculator.NextScheduledDate(rule, cursor);
        }

        if (pending.Count == 0)
        {
            return new EngineTickResult(0, 0, skipped);
        }

        db.WorkItems.AddRange(pending);

        foreach (var item in pending)
        {
            db.WorkItemEvents.Add(WorkItemEventFactory.Created(item, null, now, new
            {
                source = WorkItemSources.Engine,
                responsibilityId = responsibility.Id,
                scheduledFor = item.PeriodStart,
                catchUp = item.Status == WorkItemStatus.Missed,
            }));

            // The checklist is copied in the *same* SaveChanges as the occurrence, so the
            // (ResponsibilityId, PeriodStart) unique index that makes a double tick harmless covers the
            // checklist too: an occurrence can never exist with half a list.
            foreach (var line in template)
            {
                db.ChecklistItems.Add(ChecklistItem.FromTemplate(line, item));
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e)
        {
            // The unique index on (ResponsibilityId, PeriodStart) is the guarantee: a racing tick or
            // a second instance loses this batch harmlessly and the next tick re-derives it.
            logger.LogWarning(
                e,
                "Occurrence insert for responsibility {Id} lost a race; the rows already exist. Discarding and retrying next tick.",
                responsibility.Id);

            db.ChangeTracker.Clear();
            return new EngineTickResult(0, 0, skipped);
        }

        // Only rows a subscriber could act on. A catch-up row was inserted already-missed, and telling
        // an integration that work "appeared" for a period that closed last week is noise, not news.
        spawned.AddRange(pending.Where(item => item.Status != WorkItemStatus.Missed));

        return new EngineTickResult(created, 0, skipped);
    }

    /// <summary>
    /// Anything still open or on hold when its period ends becomes a miss. The prior status is
    /// recorded on the event so a later reader can tell "nobody touched it" apart from "it was blocked".
    /// </summary>
    private async Task<int> FlipExpiredToMissedAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Anything still outstanding when the period ends is a miss — including work in progress.
        // Starting something has never been the same as finishing it.
        var expired = await db.WorkItems
            .Where(w => w.ResponsibilityId != null
                        && WorkItemQueries.Outstanding.Contains(w.Status)
                        && w.PeriodEnd != null
                        && w.PeriodEnd <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        foreach (var item in expired)
        {
            var from = item.Status;
            item.Status = WorkItemStatus.Missed;

            db.WorkItemEvents.Add(WorkItemEventFactory.StatusChanged(item, null, now, from, WorkItemStatus.Missed, new
            {
                priorStatus = from.ToString(),
                holdReason = item.HoldReason?.ToString(),
                periodEnd = item.PeriodEnd,
            }));
        }

        await db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    /// <summary>
    /// Tells owners about misses — but only recent ones.
    ///
    /// Coming back from a fortnight of downtime records hundreds of misses in one tick, and every one
    /// of them is true. Announcing them all would be a phone buzzing three hundred times about work
    /// whose periods closed a week ago. **The ledger still has every miss**; what is suppressed is the
    /// interruption, not the record.
    ///
    /// Saved separately and swallowed on failure for the same reason it runs last: the ledger is the
    /// product, and a notification must never be able to fail one.
    /// </summary>
    private async Task AnnounceMissesAsync(DateTimeOffset now, List<WorkItem> spawned, CancellationToken cancellationToken)
    {
        var window = now.AddHours(-notificationOptions.Value.MissedNotificationWindowHours);

        // The entity name comes along because the notification body reads better with it; the whole
        // row comes along because the webhook payload needs the period and the links.
        var recentItems = await db.WorkItems.AsNoTracking()
            .Include(w => w.Entity)
            .Where(w => w.Status == WorkItemStatus.Missed && w.PeriodEnd != null && w.PeriodEnd > window && w.PeriodEnd <= now)
            .ToListAsync(cancellationToken);

        var recent = recentItems
            .Select(w => new
            {
                w.Id,
                w.Title,
                w.OwnerUserId,
                EntityName = w.Entity?.Name,
            })
            .ToList();

        if (recent.Count == 0 && spawned.Count == 0)
        {
            return;
        }

        try
        {
            // Webhooks ride in the same swallow-on-failure save, and honour the same 24-hour guard: the
            // ledger keeps every miss, the announcement of it is what gets suppressed after downtime.
            foreach (var item in spawned)
            {
                await webhooks.PublishWorkItemAsync(WebhookEventType.WorkItemCreated, item, cancellationToken);
            }

            foreach (var item in recentItems)
            {
                await webhooks.PublishWorkItemAsync(WebhookEventType.WorkItemMissed, item, cancellationToken);
            }

            if (recent.Count > 0)
            {
                await notifications.EnqueueManyAsync(
                    recent
                        .Select(item => new NotificationRequest(
                            item.OwnerUserId,
                            NotificationType.Missed,
                            item.Id,
                            Data: NotificationData.For(
                                (NotificationData.Title, item.Title),
                                (NotificationData.Entity, item.EntityName)),

                            // An occurrence goes missed once, so the key needs no date component.
                            DedupeKey: $"Missed:{item.Id}"))
                        .ToArray(),
                    cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not enqueue missed-occurrence notifications or webhooks. The ledger is unaffected.");
            db.ChangeTracker.Clear();
        }
    }
}
