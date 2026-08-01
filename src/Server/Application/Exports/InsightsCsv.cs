using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Insights;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Exports;

/// <summary>
/// The four insight tables as files. Same rule as the reports: each view dispatches the insight's own
/// query, so a rate in the file is the rate on the screen and not a second calculation of it.
///
/// Suppressed rates are exported as a <strong>blank</strong> percentage with the volume beside it, exactly
/// as the screen renders "— · 2/3". Writing 66.7 into a file for a denominator the product refuses to show
/// a percentage for would launder the very caveat the suppression exists to make.
/// </summary>
public sealed class ExportInsightHandler(ISender sender, IOptions<ExportOptions> options)
    : IRequestHandler<ExportInsightQuery, CsvDocument>
{
    public Task<CsvDocument> Handle(ExportInsightQuery request, CancellationToken cancellationToken = default)
        => request.View switch
        {
            InsightExportView.Compliance => ComplianceAsync(request, cancellationToken),
            InsightExportView.Reliability => ReliabilityAsync(request, cancellationToken),
            InsightExportView.Concentration => ConcentrationAsync(request, cancellationToken),
            InsightExportView.HoldAging => HoldAgingAsync(request, cancellationToken),
            _ => throw new ValidationException($"'{request.View}' is not an exportable insight."),
        };

    private async Task<CsvDocument> ComplianceAsync(ExportInsightQuery request, CancellationToken cancellationToken)
    {
        string[] headers =
        [
            "responsibilityId", "title", "owner", "entity", "department", "active", "paused",
            "onTime", "late", "missed", "concluded", "inFlight", "ratePercent", "rateSuppressed",
        ];

        var rows = await PagedExport.CollectAsync(
            (page, pageSize) => sender.Send(
                new ComplianceQuery(
                    request.OwnerId,
                    request.DepartmentId,
                    request.EntityId,
                    request.EntityType,
                    request.From,
                    request.To,
                    request.Bucket,
                    request.Buckets,
                    Page: page,
                    PageSize: pageSize),
                cancellationToken),
            options.Value.MaxRows);

        return new CsvDocument("compliance", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            foreach (var row in rows)
            {
                yield return
                [
                    row.ResponsibilityId.ToString(),
                    row.Title,
                    row.OwnerName,
                    row.EntityName,
                    row.DepartmentName,
                    CsvValue.Bool(row.Active),
                    CsvValue.Bool(row.Paused),
                    CsvValue.Number(row.OnTime),
                    CsvValue.Number(row.Late),
                    CsvValue.Number(row.Missed),
                    CsvValue.Number(row.Concluded),
                    CsvValue.Number(row.InFlight),
                    row.RateSuppressed ? null : CsvValue.Percent(row.Rate),
                    CsvValue.Bool(row.RateSuppressed),
                ];
            }

            await Task.CompletedTask;
        }
    }

    private async Task<CsvDocument> ReliabilityAsync(ExportInsightQuery request, CancellationToken cancellationToken)
    {
        string[] headers =
        [
            "userId", "name", "onTime", "late", "missed", "concluded", "inFlight",
            "ratePercent", "rateSuppressed", "externallyBlocked", "blockedDays",
            "oneOffCompleted", "handedOverInWindow",
        ];

        var rows = await sender.Send(
            new ReliabilityQuery(
                request.OwnerId,
                request.DepartmentId,
                request.EntityId,
                request.EntityType,
                request.From,
                request.To,
                request.Bucket,
                request.Buckets),
            cancellationToken);

        ExportGuard.EnsureWithinLimit(rows.Count, options.Value.MaxRows);

        return new CsvDocument("reliability", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            foreach (var row in rows)
            {
                yield return
                [
                    row.UserId.ToString(),
                    row.DisplayName,
                    CsvValue.Number(row.OnTime),
                    CsvValue.Number(row.Late),
                    CsvValue.Number(row.Missed),
                    CsvValue.Number(row.Concluded),
                    CsvValue.Number(row.InFlight),
                    row.RateSuppressed ? null : CsvValue.Percent(row.Rate),
                    CsvValue.Bool(row.RateSuppressed),
                    CsvValue.Number(row.ExternallyBlocked),
                    CsvValue.Decimal(row.BlockedDays),
                    CsvValue.Number(row.OneOffCompleted),
                    CsvValue.Number(row.HandedOverInWindow),
                ];
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// One row per entity <em>and bucket</em>. A wide file with a column per month would need a new header
    /// row every time the window changed; long form is what a pivot table wants.
    /// </summary>
    private async Task<CsvDocument> ConcentrationAsync(ExportInsightQuery request, CancellationToken cancellationToken)
    {
        string[] headers = ["entityId", "entity", "entityType", "bucket", "occurrences", "oneOffs", "total"];

        var series = await sender.Send(
            new ConcentrationQuery(
                request.OwnerId,
                request.DepartmentId,
                request.EntityId,
                request.EntityType,
                request.From,
                request.To,
                request.Bucket,
                request.Buckets),
            cancellationToken);

        var flattened = series.Rows
            .SelectMany(row => row.Points.Select(point => (Row: row, Point: point)))
            .ToList();

        ExportGuard.EnsureWithinLimit(flattened.Count, options.Value.MaxRows);

        return new CsvDocument("completed-work-by-entity", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            foreach (var (row, point) in flattened)
            {
                yield return
                [
                    row.EntityId.ToString(),
                    row.EntityName,
                    row.EntityType.ToString(),
                    point.BucketKey,
                    CsvValue.Number(point.Occurrences),
                    CsvValue.Number(point.OneOffs),
                    CsvValue.Number(point.Total),
                ];
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Both halves of the screen in one file, told apart by a <c>grouping</c> column — they answer the same
    /// question two ways, and splitting them into two downloads would just make them harder to compare.
    /// Days are calendar days, as everywhere else in hold aging.
    /// </summary>
    private async Task<CsvDocument> HoldAgingAsync(ExportInsightQuery request, CancellationToken cancellationToken)
    {
        string[] headers =
        [
            "grouping", "key", "entityType", "holds", "items",
            "totalWaitDays", "averageWaitDays", "longestWaitDays", "stillOnHold",
        ];

        var aging = await sender.Send(
            new HoldAgingQuery(
                request.OwnerId,
                request.DepartmentId,
                request.EntityId,
                request.EntityType,
                request.From,
                request.To,
                request.Bucket,
                request.Buckets),
            cancellationToken);

        ExportGuard.EnsureWithinLimit(aging.ByReason.Count + aging.ByEntity.Count, options.Value.MaxRows);

        return new CsvDocument("hold-aging", headers, Rows());

        async IAsyncEnumerable<string?[]> Rows()
        {
            foreach (var row in aging.ByReason)
            {
                yield return
                [
                    "reason",
                    row.Reason.ToString(),
                    null,
                    CsvValue.Number(row.Holds),
                    CsvValue.Number(row.Items),
                    CsvValue.Decimal(row.TotalWaitDays),
                    CsvValue.Decimal(row.AverageWaitDays),
                    CsvValue.Decimal(row.LongestWaitDays),
                    CsvValue.Number(row.StillOnHold),
                ];
            }

            foreach (var row in aging.ByEntity)
            {
                yield return
                [
                    "entity",
                    row.EntityName,
                    CsvValue.Enum(row.EntityType),
                    CsvValue.Number(row.Holds),
                    CsvValue.Number(row.Items),
                    CsvValue.Decimal(row.TotalWaitDays),
                    CsvValue.Decimal(row.AverageWaitDays),
                    CsvValue.Decimal(row.LongestWaitDays),
                    CsvValue.Number(row.StillOnHold),
                ];
            }

            await Task.CompletedTask;
        }
    }
}
