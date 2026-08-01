using Everdue.Server.Domain;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// The scope filters as one SQL predicate, applied identically by every insight read. Stated once so a
/// filter cannot narrow one number on a screen and be forgotten on the next — the same reason the v1
/// work-item filter is a single expression.
/// </summary>
internal static class InsightsScope
{
    public static IQueryable<WorkItem> Apply(IQueryable<WorkItem> query, InsightsFilter filter)
    {
        if (filter.OwnerId is { } ownerId)
        {
            query = query.Where(w => w.OwnerUserId == ownerId);
        }

        if (filter.DepartmentId is { } departmentId)
        {
            query = query.Where(w => w.DepartmentId == departmentId);
        }

        if (filter.EntityId is { } entityId)
        {
            query = query.Where(w => w.EntityId == entityId);
        }

        if (filter.EntityType is { } entityType)
        {
            query = query.Where(w => w.EntityId != null && w.Entity!.Type == entityType);
        }

        if (filter.ResponsibilityId is { } responsibilityId)
        {
            query = query.Where(w => w.ResponsibilityId == responsibilityId);
        }

        return query;
    }
}
