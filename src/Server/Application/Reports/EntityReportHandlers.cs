using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Checklists;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.WorkItems;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Reports;

/// <summary>Per-entity counts, computed once and reused by entity health and the neglect view.</summary>
internal sealed record EntityAggregate(
    Guid EntityId,
    int Open,
    int Overdue,
    int Missed30,
    int Missed60,
    int Missed90,
    int OnHold,
    DateTimeOffset? LastActivityAt,
    int TotalEver);

internal sealed class EntityAggregateReader(IEverdueDbContext db)
{
    /// <summary>
    /// "Last activity" is MAX(CompletedAt) over Completed and CompletedLate, and nothing else counts —
    /// which is precisely what makes the neglect report trustworthy where CRM activity logs are not.
    /// </summary>
    public async Task<Dictionary<Guid, EntityAggregate>> ReadAsync(
        ReportFilter filter,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var scope = ExceptionsReportHandler.Base(filter) with { DueFrom = filter.From, DueTo = filter.To };

        var day30 = now.AddDays(-30);
        var day60 = now.AddDays(-60);
        var day90 = now.AddDays(-90);

        var aggregates = await WorkItemQueries
            .Filter(db.WorkItems.AsNoTracking(), scope, now)
            .Where(w => w.EntityId != null)
            .GroupBy(w => w.EntityId!.Value)
            .Select(g => new EntityAggregate(
                g.Key,
                // "Open" here means outstanding, not the literal status: work someone has started is
                // still on this entity's plate, and must not vanish from the count by being started.
                g.Count(w => w.Status == WorkItemStatus.Open || w.Status == WorkItemStatus.InProgress),
                g.Count(w => WorkItemQueries.Outstanding.Contains(w.Status) && w.DueDate < now),
                g.Count(w => (w.Status == WorkItemStatus.Missed || w.Status == WorkItemStatus.CompletedLate) && w.DueDate >= day30),
                g.Count(w => (w.Status == WorkItemStatus.Missed || w.Status == WorkItemStatus.CompletedLate) && w.DueDate >= day60),
                g.Count(w => (w.Status == WorkItemStatus.Missed || w.Status == WorkItemStatus.CompletedLate) && w.DueDate >= day90),
                g.Count(w => w.Status == WorkItemStatus.OnHold),
                g.Max(w => w.Status == WorkItemStatus.Completed || w.Status == WorkItemStatus.CompletedLate ? w.CompletedAt : null),
                g.Count()))
            .ToListAsync(cancellationToken);

        return aggregates.ToDictionary(a => a.EntityId);
    }

    public static int? DaysSince(DateTimeOffset? lastActivity, DateTimeOffset now)
        => lastActivity is { } at ? (int)Math.Floor((now - at).TotalDays) : null;
}

public sealed class EntityHealthHandler(IEverdueDbContext db, IClock clock)
    : IRequestHandler<EntityHealthQuery, PagedResult<EntityHealthRowDto>>
{
    public async Task<PagedResult<EntityHealthRowDto>> Handle(EntityHealthQuery request, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);
        var filter = request.Filter;

        var aggregates = await new EntityAggregateReader(db).ReadAsync(filter, now, cancellationToken);

        var entityQuery = db.Entities.AsNoTracking().Where(e => e.Active);

        if (filter.EntityType is { } entityType)
        {
            entityQuery = entityQuery.Where(e => e.Type == entityType);
        }

        if (SearchPattern.For(request.Search) is { } pattern)
        {
            entityQuery = entityQuery.Where(e => EF.Functions.Like(e.Name.ToLower(), pattern, SearchPattern.Escape));
        }

        var entities = await entityQuery.Select(e => new { e.Id, e.Name, e.Type }).ToListAsync(cancellationToken);

        var rows = entities.Select(e =>
        {
            var a = aggregates.GetValueOrDefault(e.Id) ?? new EntityAggregate(e.Id, 0, 0, 0, 0, 0, 0, null, 0);
            return new EntityHealthRowDto(
                e.Id,
                e.Name,
                e.Type,
                a.Open,
                a.Overdue,
                a.Missed30,
                a.Missed60,
                a.Missed90,
                a.OnHold,
                a.LastActivityAt,
                EntityAggregateReader.DaysSince(a.LastActivityAt, now),
                DrillThroughFactory.For(ExceptionsReportHandler.Base(filter) with
                {
                    EntityId = e.Id,
                    EntityType = null,
                    DueFrom = filter.From,
                    DueTo = filter.To,
                }));
        });

        // Sorting happens here rather than in SQL because half the columns are computed. At the scale
        // this product targets (hundreds of entities) that is the honest trade; if an installation
        // ever outgrows it, the aggregate query is the thing to push down, not the sort.
        var sorted = Sort(rows, request.ResolvedSort, request.Descending).ToList();

        var pageItems = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return new PagedResult<EntityHealthRowDto>(pageItems, sorted.Count, page, pageSize);
    }

    private static IEnumerable<EntityHealthRowDto> Sort(IEnumerable<EntityHealthRowDto> rows, EntityHealthSort sort, bool descending)
    {
        IOrderedEnumerable<EntityHealthRowDto> ordered = sort switch
        {
            EntityHealthSort.Open => descending ? rows.OrderByDescending(r => r.Open) : rows.OrderBy(r => r.Open),
            EntityHealthSort.Overdue => descending ? rows.OrderByDescending(r => r.Overdue) : rows.OrderBy(r => r.Overdue),
            EntityHealthSort.Missed30 => descending ? rows.OrderByDescending(r => r.Missed30) : rows.OrderBy(r => r.Missed30),
            EntityHealthSort.Missed60 => descending ? rows.OrderByDescending(r => r.Missed60) : rows.OrderBy(r => r.Missed60),
            EntityHealthSort.Missed90 => descending ? rows.OrderByDescending(r => r.Missed90) : rows.OrderBy(r => r.Missed90),
            EntityHealthSort.OnHold => descending ? rows.OrderByDescending(r => r.OnHold) : rows.OrderBy(r => r.OnHold),

            // Never-touched entities are the most neglected, so a null sorts as "infinitely long ago".
            EntityHealthSort.DaysSinceLastActivity => descending
                ? rows.OrderByDescending(r => r.DaysSinceLastActivity ?? int.MaxValue)
                : rows.OrderBy(r => r.DaysSinceLastActivity ?? int.MaxValue),
            _ => descending ? rows.OrderByDescending(r => r.EntityName) : rows.OrderBy(r => r.EntityName),
        };

        return ordered.ThenBy(r => r.EntityName);
    }
}

public sealed class NeglectReportHandler(IEverdueDbContext db, IClock clock)
    : IRequestHandler<NeglectReportQuery, IReadOnlyList<NeglectRowDto>>
{
    public async Task<IReadOnlyList<NeglectRowDto>> Handle(NeglectReportQuery request, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var days = Math.Clamp(request.Days, 1, 3650);
        var filter = request.Filter;

        var aggregates = await new EntityAggregateReader(db).ReadAsync(filter, now, cancellationToken);

        var entityQuery = db.Entities.AsNoTracking().Where(e => e.Active);

        if (filter.EntityType is { } entityType)
        {
            entityQuery = entityQuery.Where(e => e.Type == entityType);
        }

        var entities = await entityQuery.Select(e => new { e.Id, e.Name, e.Type }).ToListAsync(cancellationToken);

        return entities
            // Only entities that have ever carried work: a reference row nobody ever used is unused,
            // not neglected, and listing it would bury the rows that need attention.
            .Where(e => aggregates.TryGetValue(e.Id, out var a) && a.TotalEver > 0)
            .Select(e =>
            {
                var a = aggregates[e.Id];
                return new NeglectRowDto(
                    e.Id,
                    e.Name,
                    e.Type,
                    a.LastActivityAt,
                    EntityAggregateReader.DaysSince(a.LastActivityAt, now),
                    a.Open,
                    DrillThroughFactory.For(ExceptionsReportHandler.Base(filter) with { EntityId = e.Id, EntityType = null }));
            })
            .Where(r => r.DaysSinceLastActivity is null || r.DaysSinceLastActivity >= days)
            .OrderByDescending(r => r.DaysSinceLastActivity ?? int.MaxValue)
            .ThenBy(r => r.EntityName)
            .ToArray();
    }
}

public sealed class BlockedByEntityHandler(IEverdueDbContext db, IClock clock)
    : IRequestHandler<BlockedByEntityQuery, IReadOnlyList<BlockedByEntityGroupDto>>
{
    public async Task<IReadOnlyList<BlockedByEntityGroupDto>> Handle(BlockedByEntityQuery request, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var filter = request.Filter;

        var scope = ExceptionsReportHandler.Base(filter) with
        {
            Status = WorkItemStatus.OnHold.ToString(),
            DueFrom = filter.From,
            DueTo = filter.To,
        };

        var items = await WorkItemQueries
            .Filter(db.WorkItems.AsNoTracking(), scope, now)
            .Select(w => new
            {
                w.Id,
                w.EntityId,
                EntityName = w.EntityId == null ? null : w.Entity!.Name,
                EntityType = w.EntityId == null ? null : (EntityType?)w.Entity!.Type,
                w.HoldReason,
            })
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return [];
        }

        var ids = items.Select(i => i.Id).ToArray();

        var heldSince = await db.WorkItemEvents.AsNoTracking()
            .Where(e => ids.Contains(e.WorkItemId) && e.ToStatus == WorkItemStatus.OnHold)
            .GroupBy(e => e.WorkItemId)
            .Select(g => new { WorkItemId = g.Key, At = g.Max(e => e.Timestamp) })
            .ToDictionaryAsync(x => x.WorkItemId, x => x.At, cancellationToken);

        return items
            .GroupBy(i => new { i.EntityId, i.EntityName, i.EntityType })
            .Select(group => new BlockedByEntityGroupDto(
                group.Key.EntityId,
                group.Key.EntityName ?? "—",
                group.Key.EntityType,
                group.Count(),
                Oldest(group.Select(i => i.Id), heldSince),
                group
                    .Where(i => i.HoldReason is not null)
                    .GroupBy(i => i.HoldReason!.Value)
                    .Select(byReason => new HoldReasonGroupDto(
                        byReason.Key,
                        byReason.Count(),
                        Oldest(byReason.Select(i => i.Id), heldSince),
                        DrillThroughFactory.For(scope with
                        {
                            EntityId = group.Key.EntityId,
                            EntityType = null,
                            HoldReason = byReason.Key.ToString(),
                        })))
                    .OrderByDescending(r => r.Count)
                    .ToArray(),
                DrillThroughFactory.For(scope with { EntityId = group.Key.EntityId, EntityType = null })))
            .OrderByDescending(g => g.Total)
            .ThenBy(g => g.EntityName)
            .ToArray();
    }

    private static DateTimeOffset? Oldest(IEnumerable<Guid> workItemIds, IReadOnlyDictionary<Guid, DateTimeOffset> heldSince)
    {
        var known = workItemIds.Where(heldSince.ContainsKey).Select(id => heldSince[id]).ToArray();
        return known.Length == 0 ? null : known.Min();
    }
}

public sealed class EntityTimelineHandler(IEverdueDbContext db, IClock clock, ChecklistProgressReader checklists)
    : IRequestHandler<EntityTimelineQuery, EntityTimelineDto>
{
    public async Task<EntityTimelineDto> Handle(EntityTimelineQuery request, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var entity = await db.Entities.AsNoTracking()
                         .Where(e => e.Id == request.EntityId)
                         .Select(e => new { e.Id, e.Name, e.Type })
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? throw new NotFoundException(ResourceNames.Entity, request.EntityId);

        var scope = new ListWorkItemsQuery(
            EntityId: request.EntityId,
            OwnerId: request.OwnerId,
            DepartmentId: request.DepartmentId,
            DueFrom: request.From,
            DueTo: request.To,
            IncludeCancelled: false);

        // The sort key is "period start, or due date for one-off work". Coalescing two converted
        // timestamp columns is not portable across the two providers, and an entity's history is
        // small, so the interleaving happens here rather than in SQL.
        var rows = await WorkItemQueries
            .Filter(db.WorkItems.AsNoTracking(), scope, now)
            .Select(w => new TimelineItemDto(
                w.Id,
                w.ResponsibilityId,
                w.ResponsibilityId == null ? null : w.Responsibility!.Title,
                w.Title,
                w.DueDate,
                w.PeriodStart,
                w.DueDate,
                w.Status,
                w.HoldReason,
                w.HoldReasonText,
                w.CompletedAt,
                w.ResponsibilityId != null))
            .ToListAsync(cancellationToken);

        // One grouped query for the whole timeline. An entity's inspection history is exactly where a reader wants
        // to see how much of each checklist was actually done.
        var progress = await checklists.ForAsync(rows.Select(row => row.WorkItemId), cancellationToken);

        var items = rows
            .Select(i => i with
            {
                SortDate = i.PeriodStart ?? i.DueDate,
                ChecklistTotal = progress.TryGetValue(i.WorkItemId, out var found) ? found.Total : null,
                ChecklistChecked = progress.TryGetValue(i.WorkItemId, out var checked_) ? checked_.Checked : null,
            })
            .OrderByDescending(i => i.SortDate)
            .ThenByDescending(i => i.DueDate)
            .ToArray();

        var lastActivity = items
            .Where(i => i.Status.IsCompletion() && i.CompletedAt is not null)
            .Select(i => i.CompletedAt)
            .DefaultIfEmpty(null)
            .Max();

        return new EntityTimelineDto(entity.Id, entity.Name, entity.Type, lastActivity, items);
    }
}
