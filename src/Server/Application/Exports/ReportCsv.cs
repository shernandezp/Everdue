using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Reports;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Exports;

/// <summary>
/// The three tabular reports as files.
///
/// Each view dispatches the report's own query through <see cref="ISender"/>, so every number in the file
/// has the same single definition it has on the screen — there is no second implementation of "days since
/// last activity" anywhere in this class.
///
/// The exceptions dashboard has no export: it is four integers, each of which drills through to
/// <c>/workitems</c>, which is exportable on its own.
/// </summary>
public sealed class ExportReportHandler(ISender sender, IOptions<ExportOptions> options)
    : IRequestHandler<ExportReportQuery, CsvDocument>
{
    public Task<CsvDocument> Handle(ExportReportQuery request, CancellationToken cancellationToken = default)
        => request.View switch
        {
            ReportExportView.EntityHealth => EntityHealthAsync(request, cancellationToken),
            ReportExportView.Neglect => NeglectAsync(request, cancellationToken),
            ReportExportView.BlockedByEntity => BlockedAsync(request, cancellationToken),
            _ => throw new ValidationException($"'{request.View}' is not an exportable report."),
        };

    private async Task<CsvDocument> EntityHealthAsync(ExportReportQuery request, CancellationToken cancellationToken)
    {
        string[] headers =
        [
            "entityId", "entity", "entityType", "open", "overdue",
            "missed30", "missed60", "missed90", "onHold",
            "lastActivityAt", "daysSinceLastActivity",
        ];

        var rows = await PagedExport.CollectAsync(
            (page, pageSize) => sender.Send(
                new EntityHealthQuery(
                    request.OwnerId,
                    request.DepartmentId,
                    request.EntityType,
                    request.From,
                    request.To,
                    Page: page,
                    PageSize: pageSize),
                cancellationToken),
            options.Value.MaxRows);

        return new CsvDocument("entity-health", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            foreach (var row in rows)
            {
                yield return
                [
                    row.EntityId.ToString(),
                    row.EntityName,
                    row.EntityType.ToString(),
                    CsvValue.Number(row.Open),
                    CsvValue.Number(row.Overdue),
                    CsvValue.Number(row.Missed30),
                    CsvValue.Number(row.Missed60),
                    CsvValue.Number(row.Missed90),
                    CsvValue.Number(row.OnHold),
                    CsvValue.Instant(row.LastActivityAt),
                    CsvValue.Number(row.DaysSinceLastActivity),
                ];
            }

            await Task.CompletedTask;
        }
    }

    private async Task<CsvDocument> NeglectAsync(ExportReportQuery request, CancellationToken cancellationToken)
    {
        string[] headers = ["entityId", "entity", "entityType", "lastActivityAt", "daysSinceLastActivity", "open"];

        var rows = await sender.Send(
            new NeglectReportQuery(request.Days, request.OwnerId, request.DepartmentId, request.EntityType, request.From, request.To),
            cancellationToken);

        ExportGuard.EnsureWithinLimit(rows.Count, options.Value.MaxRows);

        return new CsvDocument($"neglect-{request.Days}d", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            foreach (var row in rows)
            {
                yield return
                [
                    row.EntityId.ToString(),
                    row.EntityName,
                    row.EntityType.ToString(),
                    CsvValue.Instant(row.LastActivityAt),
                    CsvValue.Number(row.DaysSinceLastActivity),
                    CsvValue.Number(row.OpenCount),
                ];
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Flattened to one row per entity <em>and reason</em>: a nested group is not a table, and a CSV that
    /// repeats the entity name is what a pivot table wants anyway.
    /// </summary>
    private async Task<CsvDocument> BlockedAsync(ExportReportQuery request, CancellationToken cancellationToken)
    {
        string[] headers = ["entityId", "entity", "entityType", "holdReason", "count", "oldestHoldAt"];

        var groups = await sender.Send(
            new BlockedByEntityQuery(request.OwnerId, request.DepartmentId, request.EntityType, request.From, request.To),
            cancellationToken);

        var flattened = groups
            .SelectMany(group => group.Reasons.Select(reason => (Group: group, Reason: reason)))
            .ToList();

        ExportGuard.EnsureWithinLimit(flattened.Count, options.Value.MaxRows);

        return new CsvDocument("blocked-by-entity", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            foreach (var (group, reason) in flattened)
            {
                yield return
                [
                    group.EntityId?.ToString(),
                    group.EntityName,
                    CsvValue.Enum(group.EntityType),
                    reason.Reason.ToString(),
                    CsvValue.Number(reason.Count),
                    CsvValue.Instant(reason.OldestHoldAt),
                ];
            }

            await Task.CompletedTask;
        }
    }
}

/// <summary>
/// Walks a paged report to the end.
///
/// Page size stays at the API's own maximum rather than being raised for exports: entity health returns a
/// row per entity and compliance a row per responsibility, so this is a handful of reads, and reusing the
/// shipped handler verbatim is worth more than saving them.
/// </summary>
internal static class PagedExport
{
    public static async Task<List<T>> CollectAsync<T>(
        Func<int, int, Task<PagedResult<T>>> fetch,
        int max)
    {
        var collected = new List<T>();
        var page = 1;

        while (true)
        {
            var result = await fetch(page, Paging.MaxPageSize);

            ExportGuard.EnsureWithinLimit(result.TotalCount, max);

            collected.AddRange(result.Items);

            if (collected.Count >= result.TotalCount || result.Items.Count == 0)
            {
                return collected;
            }

            page++;
        }
    }
}
