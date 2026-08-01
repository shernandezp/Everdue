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
/// Which entities the team's completed work goes to, month by month.
///
/// It is a **count of completed work items**, not effort in hours: the ledger holds no time, so a
/// two-minute call and a full-day inspection both count as one. That is the honest limit of what this
/// data can say, and the screens say "completed work" rather than "effort" because of it. Occurrences
/// and one-off work are counted separately in the same point — both consume the same capacity, and
/// splitting them keeps the honest reading available.
/// </summary>
public sealed class ConcentrationHandler(
    IEverdueDbContext db,
    ITenantProvider tenants,
    IClock clock,
    IOptions<InsightsOptions> options)
    : IRequestHandler<ConcentrationQuery, ConcentrationSeriesDto>
{
    private sealed record Completion(Guid? EntityId, DateTimeOffset CompletedAt, bool IsOccurrence);

    public async Task<ConcentrationSeriesDto> Handle(
        ConcentrationQuery request,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var settings = options.Value;
        var timeZone = await tenants.GetTimeZoneAsync(cancellationToken);

        var filter = request.Scope();
        var window = request.Window(BucketKind.Month, timeZone, now, settings);

        var completions = await InsightsScope.Apply(
                db.WorkItems.AsNoTracking()
                    .Where(w => (w.Status == WorkItemStatus.Completed || w.Status == WorkItemStatus.CompletedLate)
                                && w.CompletedAt != null
                                && w.CompletedAt >= window.From
                                && w.CompletedAt < window.To),
                filter)
            .Select(w => new Completion(w.EntityId, w.CompletedAt!.Value, w.ResponsibilityId != null))
            .ToListAsync(cancellationToken);

        var axis = window.Buckets
            .Select(bucket => new BucketAxisDto(bucket.Key, bucket.Label, bucket.Start, window.IsPartial(bucket)))
            .ToArray();

        var byEntity = completions
            .Where(completion => completion.EntityId is not null)
            .GroupBy(completion => completion.EntityId!.Value)
            .ToArray();

        var names = await EntityNamesAsync(byEntity.Select(group => group.Key).ToArray(), cancellationToken);

        var rows = byEntity
            .Where(group => names.ContainsKey(group.Key))
            .Select(group => Row(group.Key, names[group.Key], group, axis, filter, window))
            .OrderByDescending(row => row.Total)
            .ThenBy(row => row.EntityName)
            .ToArray();

        return new ConcentrationSeriesDto(
            axis,
            rows.Take(settings.TopEntities).ToArray(),
            Math.Max(0, rows.Length - settings.TopEntities),

            // Work nobody linked to an entity cannot appear in any row. Reporting how much of it there
            // is beats letting it quietly skew the picture — the chart is only as good as the linking.
            completions.Count(completion => completion.EntityId is null));
    }

    private ConcentrationRowDto Row(
        Guid entityId,
        (string Name, EntityType Type) entity,
        IEnumerable<Completion> completions,
        IReadOnlyList<BucketAxisDto> axis,
        InsightsFilter filter,
        InsightsWindow window)
    {
        var byBucket = completions
            .GroupBy(completion => window.KeyFor(completion.CompletedAt))
            .ToDictionary(
                group => group.Key,
                group => (Occurrences: group.Count(c => c.IsOccurrence), OneOffs: group.Count(c => !c.IsOccurrence)));

        // Dense on purpose: a month with no completions is a zero, never a gap. A chart that skips
        // quiet months tells a different story from the one the data supports.
        var points = axis
            .Select(bucket =>
            {
                var counts = byBucket.GetValueOrDefault(bucket.Key);
                return new ConcentrationPointDto(bucket.Key, counts.Occurrences, counts.OneOffs);
            })
            .ToArray();

        return new ConcentrationRowDto(
            entityId,
            entity.Name,
            entity.Type,
            points.Sum(point => point.Total),
            points,
            DrillThroughFactory.For(ListWorkItemsQuery.For(
                ownerId: filter.OwnerId,
                departmentId: filter.DepartmentId,
                entityId: entityId,
                statuses: [WorkItemStatus.Completed, WorkItemStatus.CompletedLate]) with
            {
                CompletedFrom = window.From,
                CompletedTo = window.To.AddTicks(-1),
            }));
    }

    private async Task<Dictionary<Guid, (string Name, EntityType Type)>> EntityNamesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await db.Entities.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.Name, e.Type })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id, row => (row.Name, row.Type));
    }
}
