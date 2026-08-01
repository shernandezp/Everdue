using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Checklists;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Exports;

/// <summary>
/// The work list as a file.
///
/// It reads through <see cref="WorkItemQueries.Filter"/> — the single definition of what every filter
/// parameter means, and the same expression the list page and every drill-through go through. That shared
/// predicate, not a promise in a tooltip, is what makes the file match the screen. Paging is deliberately
/// not applied: an export is the whole result or a refusal, never page one.
/// </summary>
public sealed class ExportWorkItemsHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    IClock clock,
    ChecklistProgressReader checklists,
    IOptions<ExportOptions> options) : IRequestHandler<ExportWorkItemsQuery, CsvDocument>
{
    private static readonly string[] Headers =
    [
        "id", "kind", "responsibilityId", "responsibilityTitle", "title", "description",
        "owner", "entity", "entityType", "department",
        "dueDate", "periodStart", "periodEnd", "status", "isOverdue",
        "holdReason", "holdReasonText", "createdAt", "completedAt", "completedBy",
        "checklistChecked", "checklistTotal",
    ];

    public async Task<CsvDocument> Handle(ExportWorkItemsQuery request, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var query = WorkItemQueries.Filter(db.WorkItems.AsNoTracking(), request.Filter, now);

        ExportGuard.EnsureWithinLimit(await query.CountAsync(cancellationToken), options.Value.MaxRows);

        var rows = await WorkItemQueries
            .Project(query.OrderBy(w => w.DueDate).ThenBy(w => w.Id))
            .ToListAsync(cancellationToken);

        var items = await WorkItemQueries.ToDtosAsync(rows, users, now, cancellationToken, checklists);

        return new CsvDocument("workitems", Headers, Rows(items));
    }

    private static async IAsyncEnumerable<string?[]> Rows(IReadOnlyList<WorkItemDto> items)
    {
        foreach (var item in items)
        {
            yield return
            [
                item.Id.ToString(),
                item.ResponsibilityId is null ? "task" : "occurrence",
                item.ResponsibilityId?.ToString(),
                item.ResponsibilityTitle,
                item.Title,
                item.Description,
                item.OwnerDisplayName,
                item.EntityName,
                CsvValue.Enum(item.EntityType),
                item.DepartmentName,
                CsvValue.Instant(item.DueDate),
                CsvValue.Instant(item.PeriodStart),
                CsvValue.Instant(item.PeriodEnd),
                item.Status.ToString(),
                CsvValue.Bool(item.IsOverdue),
                CsvValue.Enum(item.HoldReason),
                item.HoldReasonText,
                CsvValue.Instant(item.CreatedAt),
                CsvValue.Instant(item.CompletedAt),
                item.CompletedByDisplayName,
                CsvValue.Number(item.ChecklistChecked),
                CsvValue.Number(item.ChecklistTotal),
            ];
        }

        await Task.CompletedTask;
    }
}
