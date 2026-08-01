using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Checklists;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.WorkItems;

/// <summary>
/// Flat projection of a work item plus the names it displays with. Reports reuse it so the numbers
/// on a dashboard and the rows behind them come out of the same expression.
/// </summary>
public sealed record WorkItemRow(
    Guid Id,
    Guid? ResponsibilityId,
    string? ResponsibilityTitle,
    string Title,
    string? Description,
    Guid OwnerUserId,
    Guid? EntityId,
    string? EntityName,
    EntityType? EntityType,
    Guid? DepartmentId,
    string? DepartmentName,
    DateTimeOffset DueDate,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    WorkItemStatus Status,
    HoldReason? HoldReason,
    string? HoldReasonText,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    Guid? CompletedByUserId);

public static class WorkItemQueries
{
    /// <summary>
    /// The statuses that mean "still on someone's plate". Kept as an array because EF translates
    /// <c>Contains</c> into an IN clause, and stated once so a new state cannot be forgotten in one
    /// query and remembered in another.
    /// </summary>
    internal static readonly WorkItemStatus[] Outstanding =
        [WorkItemStatus.Open, WorkItemStatus.InProgress, WorkItemStatus.OnHold];

    /// <summary>
    /// Outstanding plus Missed: everything somebody could still act on. A missed occurrence still
    /// needs completing late, so a hand-over that skipped it would leave exactly the work that
    /// matters most with the person who has gone.
    /// </summary>
    internal static readonly WorkItemStatus[] Workable =
        [WorkItemStatus.Open, WorkItemStatus.InProgress, WorkItemStatus.OnHold, WorkItemStatus.Missed];

    public static IQueryable<WorkItemRow> Project(IQueryable<WorkItem> query)
        => query.Select(w => new WorkItemRow(
            w.Id,
            w.ResponsibilityId,
            w.ResponsibilityId == null ? null : w.Responsibility!.Title,
            w.Title,
            w.Description,
            w.OwnerUserId,
            w.EntityId,
            w.EntityId == null ? null : w.Entity!.Name,
            w.EntityId == null ? null : w.Entity!.Type,
            w.DepartmentId,
            w.DepartmentId == null ? null : w.Department!.Name,
            w.DueDate,
            w.PeriodStart,
            w.PeriodEnd,
            w.Status,
            w.HoldReason,
            w.HoldReasonText,
            w.CreatedAt,
            w.CompletedAt,
            w.CompletedByUserId));

    /// <summary>
    /// Applies the shared filter vocabulary. Cancelled rows are excluded unless asked for: a task
    /// that no longer applies must never show up in a count.
    /// </summary>
    public static IQueryable<WorkItem> Filter(IQueryable<WorkItem> query, ListWorkItemsQuery filter, DateTimeOffset now)
    {
        if (!filter.IncludeCancelled)
        {
            query = query.Where(w => w.Status != WorkItemStatus.Cancelled);
        }

        if (filter.ResolvedStatuses is { Length: > 0 } statuses)
        {
            query = query.Where(w => statuses.Contains(w.Status));
        }

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

        if (filter.ResponsibilityId is { } responsibilityId)
        {
            query = query.Where(w => w.ResponsibilityId == responsibilityId);
        }

        if (filter.Occurrences is { } occurrencesOnly)
        {
            query = occurrencesOnly
                ? query.Where(w => w.ResponsibilityId != null)
                : query.Where(w => w.ResponsibilityId == null);
        }

        if (filter.ResolvedEntityType is { } entityType)
        {
            query = query.Where(w => w.EntityId != null && w.Entity!.Type == entityType);
        }

        if (filter.ResolvedHoldReason is { } holdReason)
        {
            query = query.Where(w => w.HoldReason == holdReason);
        }

        if (filter.DueFrom is { } dueFrom)
        {
            query = query.Where(w => w.DueDate >= dueFrom);
        }

        if (filter.DueTo is { } dueTo)
        {
            query = query.Where(w => w.DueDate <= dueTo);
        }

        if (filter.CompletedFrom is { } completedFrom)
        {
            query = query.Where(w => w.CompletedAt != null && w.CompletedAt >= completedFrom);
        }

        if (filter.CompletedTo is { } completedTo)
        {
            query = query.Where(w => w.CompletedAt != null && w.CompletedAt <= completedTo);
        }

        if (filter.Overdue == true)
        {
            // Derived, never stored. Work someone has started is still overdue if it is past due —
            // picking something up must never make it disappear from a manager's overdue count.
            query = query.Where(w => Outstanding.Contains(w.Status) && w.DueDate < now);
        }

        if (SearchPattern.For(filter.Search) is { } pattern)
        {
            query = query.Where(w => EF.Functions.Like(w.Title.ToLower(), pattern, SearchPattern.Escape));
        }

        if (filter.ResolvedView == WorkItemView.Board)
        {
            var doneSince = now.AddDays(-7);
            query = query.Where(w =>
                Outstanding.Contains(w.Status)
                || w.Status == WorkItemStatus.Missed
                || ((w.Status == WorkItemStatus.Completed || w.Status == WorkItemStatus.CompletedLate)
                    && w.CompletedAt != null && w.CompletedAt >= doneSince));
        }

        return query;
    }

    /// <summary>Stable order: whatever the caller sorts by, ties break on Id so paging never repeats a row.</summary>
    public static IOrderedQueryable<WorkItem> Sort(IQueryable<WorkItem> query, WorkItemSort sort, bool descending)
    {
        var ordered = sort switch
        {
            WorkItemSort.Title => descending
                ? query.OrderByDescending(w => w.Title)
                : query.OrderBy(w => w.Title),
            WorkItemSort.Status => descending
                ? query.OrderByDescending(w => w.Status)
                : query.OrderBy(w => w.Status),
            WorkItemSort.Entity => descending
                ? query.OrderByDescending(w => w.EntityId == null ? null : w.Entity!.Name)
                : query.OrderBy(w => w.EntityId == null ? null : w.Entity!.Name),
            _ => descending
                ? query.OrderByDescending(w => w.DueDate)
                : query.OrderBy(w => w.DueDate),
        };

        return ordered.ThenBy(w => w.Id);
    }

    public static WorkItemDto ToDto(
        WorkItemRow row,
        IReadOnlyDictionary<Guid, UserSummary> users,
        DateTimeOffset now,
        IReadOnlyDictionary<Guid, ChecklistProgress>? checklists = null)
    {
        var progress = checklists is not null && checklists.TryGetValue(row.Id, out var found) ? found : null;

        return new WorkItemDto(
            row.Id,
            row.ResponsibilityId,
            row.ResponsibilityTitle,
            row.Title,
            row.Description,
            row.OwnerUserId,
            users.TryGetValue(row.OwnerUserId, out var owner) ? owner.DisplayName : "—",
            row.EntityId,
            row.EntityName,
            row.EntityType,
            row.DepartmentId,
            row.DepartmentName,
            row.DueDate,
            row.PeriodStart,
            row.PeriodEnd,
            row.Status,
            row.HoldReason,
            row.HoldReasonText,
            row.Status.IsOutstanding() && now > row.DueDate,
            row.CreatedAt,
            row.CompletedAt,
            row.CompletedByUserId,
            row.CompletedByUserId is { } completedBy && users.TryGetValue(completedBy, out var user) ? user.DisplayName : null,
            progress?.Total,
            progress?.Checked);
    }

    /// <summary>
    /// One page of rows into DTOs. Checklist progress is fetched for the whole page in a single grouped
    /// query, so a board of a hundred cards costs one extra query rather than a hundred.
    /// </summary>
    public static async Task<IReadOnlyList<WorkItemDto>> ToDtosAsync(
        IReadOnlyList<WorkItemRow> rows,
        IUserDirectory directory,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        ChecklistProgressReader? checklists = null)
    {
        var ids = rows.Select(r => r.OwnerUserId).Concat(rows.Where(r => r.CompletedByUserId is not null).Select(r => r.CompletedByUserId!.Value));
        var users = await directory.MapAsync(ids, cancellationToken);

        var progress = checklists is null
            ? ChecklistProgressReader.None
            : await checklists.ForAsync(rows.Select(r => r.Id), cancellationToken);

        return rows.Select(r => ToDto(r, users, now, progress)).ToArray();
    }
}
