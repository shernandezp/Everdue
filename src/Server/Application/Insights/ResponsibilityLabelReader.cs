using Everdue.Server.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// What a responsibility is called and who owns it *now* — the labels every compliance row and every
/// chronic row is displayed with. Read from the responsibility rather than from its occurrences: the
/// thing being measured is the obligation, and its current owner is who a manager would talk to.
/// </summary>
public sealed record ResponsibilityLabel(
    Guid Id,
    string Title,
    Guid OwnerUserId,
    Guid? EntityId,
    string? EntityName,
    string? DepartmentName,
    bool Active,
    DateTimeOffset? PausedUntil);

internal sealed class ResponsibilityLabelReader(IEverdueDbContext db)
{
    public async Task<Dictionary<Guid, ResponsibilityLabel>> ForAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var labels = await db.Responsibilities.AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .Select(r => new ResponsibilityLabel(
                r.Id,
                r.Title,
                r.OwnerUserId,
                r.EntityId,
                r.EntityId == null ? null : r.Entity!.Name,
                r.DepartmentId == null ? null : r.Department!.Name,
                r.Active,
                r.PausedUntil))
            .ToListAsync(cancellationToken);

        return labels.ToDictionary(label => label.Id);
    }
}
