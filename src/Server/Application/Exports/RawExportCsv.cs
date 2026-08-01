using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Entities;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Exports;

/// <summary>
/// A straight dump of one table, for analysis in a spreadsheet or a notebook.
///
/// Uncapped and streamed with <c>AsAsyncEnumerable</c>: these rows carry no aggregation that could be
/// wrong, so the honest failure mode is a large file rather than a refusal. Ids travel raw — this output is
/// for joining, not for reading.
///
/// The entity dump includes custom-field values, because a backup that cannot round-trip is not a backup.
/// It is the only place in the product where a custom field leaves the entity screen.
/// </summary>
public sealed class ExportRawTableHandler(IEverdueDbContext db, EntityCustomFieldWriter customFields)
    : IRequestHandler<ExportRawTableQuery, CsvDocument>
{
    public async Task<CsvDocument> Handle(ExportRawTableQuery request, CancellationToken cancellationToken = default)
        => request.Table switch
        {
            RawExportTable.Entities => await EntitiesAsync(cancellationToken),
            RawExportTable.Responsibilities => Responsibilities(),
            RawExportTable.WorkItems => WorkItems(),
            RawExportTable.WorkItemEvents => WorkItemEvents(),
            RawExportTable.Comments => Comments(),
            RawExportTable.ChecklistItems => ChecklistItems(),
            _ => throw new ValidationException($"'{request.Table}' is not an exportable table."),
        };

    private async Task<CsvDocument> EntitiesAsync(CancellationToken cancellationToken)
    {
        var definitions = await customFields.AllDefinitionsAsync(cancellationToken);

        // A column per definition, named after it, so the file is readable and re-importable.
        var fieldColumns = definitions
            .Select(d => $"{d.EntityType}:{d.Name}")
            .ToArray();

        string[] headers = ["id", "name", "type", "active", .. fieldColumns];

        return new CsvDocument("entities", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            var query = db.Entities.AsNoTracking()
                .OrderBy(e => e.Type)
                .ThenBy(e => e.Name)
                .Select(e => new { e.Id, e.Name, e.Type, e.Active, e.CustomFieldsJson })
                .AsAsyncEnumerable();

            await foreach (var entity in query.WithCancellation(cancellationToken))
            {
                var values = EntityCustomFields.Parse(entity.CustomFieldsJson);

                yield return
                [
                    entity.Id.ToString(),
                    entity.Name,
                    entity.Type.ToString(),
                    CsvValue.Bool(entity.Active),
                    .. definitions.Select(d => values.TryGetValue(d.Id, out var value) ? value : null),
                ];
            }
        }
    }

    private CsvDocument Responsibilities()
    {
        string[] headers =
        [
            "id", "title", "description", "ownerUserId", "departmentId", "entityId",
            "recurrenceKind", "daysOfWeekMask", "dayOfMonth", "monthOfYear", "startDate",
            "active", "pausedUntil", "requireChecklistToComplete", "requireAttachmentToComplete",
        ];

        return new CsvDocument("responsibilities", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            await foreach (var r in db.Responsibilities.AsNoTracking().OrderBy(r => r.Title).AsAsyncEnumerable())
            {
                yield return
                [
                    r.Id.ToString(),
                    r.Title,
                    r.Description,
                    r.OwnerUserId.ToString(),
                    r.DepartmentId?.ToString(),
                    r.EntityId?.ToString(),
                    r.RecurrenceKind.ToString(),
                    CsvValue.Number(r.DaysOfWeekMask),
                    CsvValue.Number(r.DayOfMonth),
                    CsvValue.Number(r.MonthOfYear),
                    CsvValue.Date(r.StartDate),
                    CsvValue.Bool(r.Active),
                    CsvValue.Instant(r.PausedUntil),
                    CsvValue.Bool(r.RequireChecklistToComplete),
                    CsvValue.Bool(r.RequireAttachmentToComplete),
                ];
            }
        }
    }

    private CsvDocument WorkItems()
    {
        string[] headers =
        [
            "id", "responsibilityId", "title", "description", "ownerUserId", "entityId", "departmentId",
            "dueDate", "periodStart", "periodEnd", "status", "holdReason", "holdReasonText",
            "createdAt", "completedAt", "completedByUserId",
        ];

        return new CsvDocument("workitems-raw", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            await foreach (var w in db.WorkItems.AsNoTracking().OrderBy(w => w.Id).AsAsyncEnumerable())
            {
                yield return
                [
                    w.Id.ToString(),
                    w.ResponsibilityId?.ToString(),
                    w.Title,
                    w.Description,
                    w.OwnerUserId.ToString(),
                    w.EntityId?.ToString(),
                    w.DepartmentId?.ToString(),
                    CsvValue.Instant(w.DueDate),
                    CsvValue.Instant(w.PeriodStart),
                    CsvValue.Instant(w.PeriodEnd),
                    w.Status.ToString(),
                    CsvValue.Enum(w.HoldReason),
                    w.HoldReasonText,
                    CsvValue.Instant(w.CreatedAt),
                    CsvValue.Instant(w.CompletedAt),
                    w.CompletedByUserId?.ToString(),
                ];
            }
        }
    }

    private CsvDocument WorkItemEvents()
    {
        string[] headers = ["id", "workItemId", "userId", "timestamp", "eventType", "fromStatus", "toStatus", "dataJson"];

        return new CsvDocument("workitem-events", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            await foreach (var e in db.WorkItemEvents.AsNoTracking().OrderBy(e => e.Timestamp).ThenBy(e => e.Id).AsAsyncEnumerable())
            {
                yield return
                [
                    e.Id.ToString(),
                    e.WorkItemId.ToString(),
                    e.UserId?.ToString(),
                    CsvValue.Instant(e.Timestamp),
                    e.EventType.ToString(),
                    CsvValue.Enum(e.FromStatus),
                    CsvValue.Enum(e.ToStatus),
                    e.DataJson,
                ];
            }
        }
    }

    private CsvDocument Comments()
    {
        string[] headers = ["id", "workItemId", "userId", "body", "createdAt"];

        return new CsvDocument("comments", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            await foreach (var c in db.Comments.AsNoTracking().OrderBy(c => c.CreatedAt).AsAsyncEnumerable())
            {
                yield return
                [
                    c.Id.ToString(),
                    c.WorkItemId.ToString(),
                    c.UserId.ToString(),
                    c.Body,
                    CsvValue.Instant(c.CreatedAt),
                ];
            }
        }
    }

    private CsvDocument ChecklistItems()
    {
        string[] headers = ["id", "workItemId", "text", "required", "position", "checkedAt", "checkedByUserId"];

        return new CsvDocument("checklist-items", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            await foreach (var c in db.ChecklistItems.AsNoTracking().OrderBy(c => c.WorkItemId).ThenBy(c => c.Position).AsAsyncEnumerable())
            {
                yield return
                [
                    c.Id.ToString(),
                    c.WorkItemId.ToString(),
                    c.Text,
                    CsvValue.Bool(c.Required),
                    CsvValue.Number(c.Position),
                    CsvValue.Instant(c.CheckedAt),
                    c.CheckedByUserId?.ToString(),
                ];
            }
        }
    }
}
