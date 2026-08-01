using Everdue.Server.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Checklists;

/// <summary>How much of one item's checklist is done, and how much of it is in the way of finishing.</summary>
public sealed record ChecklistProgress(int Total, int Checked, int RequiredOpen);

/// <summary>
/// Answers checklist progress for a <em>set</em> of work items in one grouped query.
///
/// The board, the list, the drawer and the entity timeline all show a progress badge, and every one of
/// them renders many rows. A per-row lookup would turn a flat query count into an N+1 — the exact
/// property the performance tests pin — so the only shape offered here is the batched one.
/// </summary>
public sealed class ChecklistProgressReader(IEverdueDbContext db)
{
    public static readonly IReadOnlyDictionary<Guid, ChecklistProgress> None = new Dictionary<Guid, ChecklistProgress>();

    public async Task<IReadOnlyDictionary<Guid, ChecklistProgress>> ForAsync(
        IEnumerable<Guid> workItemIds,
        CancellationToken cancellationToken)
    {
        var ids = workItemIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return None;
        }

        var rows = await db.ChecklistItems.AsNoTracking()
            .Where(c => ids.Contains(c.WorkItemId))
            .GroupBy(c => c.WorkItemId)
            .Select(g => new
            {
                WorkItemId = g.Key,
                Total = g.Count(),
                Checked = g.Count(c => c.CheckedAt != null),
                RequiredOpen = g.Count(c => c.Required && c.CheckedAt == null),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.WorkItemId, r => new ChecklistProgress(r.Total, r.Checked, r.RequiredOpen));
    }

    public async Task<ChecklistProgress?> ForOneAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var map = await ForAsync([workItemId], cancellationToken);
        return map.TryGetValue(workItemId, out var progress) ? progress : null;
    }
}
