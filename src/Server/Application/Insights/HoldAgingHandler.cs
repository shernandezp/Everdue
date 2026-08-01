using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Reports;
using Everdue.Server.Application.WorkItems;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Insights;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// Where waiting time actually goes: calendar days spent on hold, per reason and per entity,
/// reconstructed from the event log — so it answers for history nobody was recording for reports.
///
/// Covers one-off work as well as occurrences: "how long do we wait on customers" is not a question
/// about recurrence.
/// </summary>
public sealed class HoldAgingHandler(
    IEverdueDbContext db,
    ITenantProvider tenants,
    IClock clock,
    IOptions<InsightsOptions> options)
    : IRequestHandler<HoldAgingQuery, HoldAgingDto>
{
    private const int IdBatch = 500;

    private sealed record HeldItem(Guid Id, Guid? EntityId, string? EntityName, EntityType? EntityType, WorkItemStatus Status);

    public async Task<HoldAgingDto> Handle(HoldAgingQuery request, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var settings = options.Value;
        var timeZone = await tenants.GetTimeZoneAsync(cancellationToken);

        var filter = request.Scope();
        var window = request.Window(BucketKind.Month, timeZone, now, settings);
        var to = window.EffectiveTo(now);

        var clipped = (await new HoldIntervalReader(db).ReadAsync(window.To, now, cancellationToken))
            .Select(interval => interval.Clip(window.From, to))
            .OfType<HoldInterval>()
            .ToArray();

        var items = await HeldItemsAsync(clipped.Select(i => i.WorkItemId).Distinct().ToArray(), filter, cancellationToken);
        var inScope = clipped.Where(interval => items.ContainsKey(interval.WorkItemId)).ToArray();

        // A hold that has ended leaves no trace on the work item — releasing, starting or completing
        // clears the reason — so only the holds still running can be listed, and only when the window
        // actually ends now. Wait times deliberately carry no link rather than a dead one.
        var linkable = window.To >= now;

        var byReason = inScope
            .GroupBy(interval => interval.Reason)
            .Select(group => new HoldAgingRowDto(
                group.Key,
                group.Count(),
                group.Select(i => i.WorkItemId).Distinct().Count(),
                Round(group.Sum(i => i.Days)),
                Round(group.Average(i => i.Days)),
                Round(group.Max(i => i.Days)),
                group.Count(i => i.Open),
                linkable && group.Any(i => i.Open)
                    ? DrillThroughFactory.For(ListWorkItemsQuery.For(
                        ownerId: filter.OwnerId,
                        departmentId: filter.DepartmentId,
                        entityId: filter.EntityId,
                        entityType: filter.EntityType,
                        statuses: [WorkItemStatus.OnHold],
                        holdReason: group.Key))
                    : null))
            .OrderByDescending(row => row.TotalWaitDays)
            .ToArray();

        var byEntity = inScope
            .GroupBy(interval => EntityKeyOf(items[interval.WorkItemId]))
            .Select(group => new HoldAgingEntityRowDto(
                group.Key.Id,
                group.Key.Name,
                group.Key.Type,
                group.Count(),
                group.Select(i => i.WorkItemId).Distinct().Count(),
                Round(group.Sum(i => i.Days)),
                Round(group.Average(i => i.Days)),
                Round(group.Max(i => i.Days)),
                group.Count(i => i.Open)))
            .OrderByDescending(row => row.TotalWaitDays)
            .ThenBy(row => row.EntityName)
            .ToArray();

        return new HoldAgingDto(
            window.From,
            to,
            byReason,
            byEntity.Take(settings.TopEntities).ToArray(),
            Math.Max(0, byEntity.Length - settings.TopEntities));
    }

    /// <summary>Calendar days to one decimal. Nights and weekends are inside them, and the screen says so.</summary>
    private static double Round(double days) => Math.Round(days, 1, MidpointRounding.AwayFromZero);

    /// <summary>Work with no entity link still waits; it groups under one unlinked row rather than vanishing.</summary>
    private static (Guid? Id, string Name, EntityType? Type) EntityKeyOf(HeldItem item)
        => item.EntityId is { } id ? (id, item.EntityName ?? "—", item.EntityType) : (null, "—", null);

    /// <summary>
    /// The items behind the intervals, narrowed by the report's scope. Ids are queried in batches
    /// because SQLite binds one parameter per value and has a hard ceiling on how many it will take.
    /// </summary>
    private async Task<Dictionary<Guid, HeldItem>> HeldItemsAsync(
        IReadOnlyList<Guid> ids,
        InsightsFilter filter,
        CancellationToken cancellationToken)
    {
        var found = new Dictionary<Guid, HeldItem>();

        foreach (var batch in ids.Chunk(IdBatch))
        {
            var query = InsightsScope.Apply(
                db.WorkItems.AsNoTracking()
                    .Where(w => batch.Contains(w.Id) && w.Status != WorkItemStatus.Cancelled),
                filter);

            var rows = await query
                .Select(w => new HeldItem(
                    w.Id,
                    w.EntityId,
                    w.EntityId == null ? null : w.Entity!.Name,
                    w.EntityId == null ? null : (EntityType?)w.Entity!.Type,
                    w.Status))
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                found[row.Id] = row;
            }
        }

        return found;
    }
}
