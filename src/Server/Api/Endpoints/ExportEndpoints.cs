using System.Globalization;
using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Exports;
using Everdue.Server.Application.WorkItems;

namespace Everdue.Server.Api.Endpoints;

public static class ExportEndpoints
{
    /// <summary>
    /// <strong>An export never widens what its source endpoint allows.</strong> The work list is readable by
    /// anybody signed in, so its export is too; the reports and insights are administrator-only, so theirs are
    /// as well. Same policy, same tenant filter, same role check — stated here because this is the one file
    /// where getting it wrong would be invisible.
    /// </summary>
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/exports").WithTags("Exports").AllowApiKey();

        group.MapGet("/workitems", async (
                    [AsParameters] ListWorkItemsQuery filter,
                    ISender sender,
                    HttpContext context,
                    CancellationToken cancellationToken)
                => await WriteAsync(sender, new ExportWorkItemsQuery(filter), context, cancellationToken))
            .RequireAuthorization()
            .WithSummary("Exactly the rows the work list shows for the same filters.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/reports/{view}", async (
                    string view,
                    [AsParameters] ReportExportParameters parameters,
                    ISender sender,
                    HttpContext context,
                    CancellationToken cancellationToken)
                => await WriteAsync(sender, parameters.ToQuery(view), context, cancellationToken))
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("entity-health | neglect | blocked-by-entity");

        group.MapGet("/insights/{view}", async (
                    string view,
                    [AsParameters] InsightExportParameters parameters,
                    ISender sender,
                    HttpContext context,
                    CancellationToken cancellationToken)
                => await WriteAsync(sender, parameters.ToQuery(view), context, cancellationToken))
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("compliance | reliability | concentration | hold-aging");

        group.MapGet("/raw/{table}", async (
                    string table,
                    ISender sender,
                    HttpContext context,
                    CancellationToken cancellationToken)
                => await WriteAsync(
                    sender,
                    new ExportRawTableQuery(EnumQuery.Parse<RawExportTable>(Dehyphenate(table), "table")
                                            ?? throw new ValidationException("A table name is required.")),
                    context,
                    cancellationToken))
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("A streamed table dump for analysis. Uncapped: there is no aggregation here to be wrong.");

        return api;
    }

    /// <summary>
    /// Streams straight to the response body rather than buffering a file. The document's rows are an
    /// <c>IAsyncEnumerable</c> for exactly this reason.
    /// </summary>
    private static async Task<IResult> WriteAsync(
        ISender sender,
        IQuery<CsvDocument> query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var document = await sender.Send(query, cancellationToken);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        context.Response.ContentType = CsvTextWriter.ContentType;
        context.Response.Headers.ContentDisposition = $"attachment; filename=\"everdue-{document.FileName}-{stamp}.csv\"";
        context.Response.Headers.CacheControl = "private, no-store";

        await CsvTextWriter.WriteAsync(document, context.Response.Body, cancellationToken);

        return Results.Empty;
    }

    /// <summary>Route segments are hyphenated for readability; the enums are not.</summary>
    private static string Dehyphenate(string value) => value.Replace("-", string.Empty);

    /// <summary>The report filter, bound from the query string exactly as the report endpoints bind it.</summary>
    public sealed record ReportExportParameters(
        Guid? OwnerId = null,
        Guid? DepartmentId = null,
        string? EntityType = null,
        DateTimeOffset? From = null,
        DateTimeOffset? To = null,
        int Days = 90)
    {
        public ExportReportQuery ToQuery(string view)
            => new(
                EnumQuery.Parse<ReportExportView>(Dehyphenate(view), "view")
                ?? throw new ValidationException("A report view is required."),
                OwnerId,
                DepartmentId,
                EntityType,
                From,
                To,
                Days);
    }

    public sealed record InsightExportParameters(
        Guid? OwnerId = null,
        Guid? DepartmentId = null,
        Guid? EntityId = null,
        string? EntityType = null,
        DateTimeOffset? From = null,
        DateTimeOffset? To = null,
        string? Bucket = null,
        int? Buckets = null)
    {
        public ExportInsightQuery ToQuery(string view)
            => new(
                EnumQuery.Parse<InsightExportView>(Dehyphenate(view), "view")
                ?? throw new ValidationException("An insight view is required."),
                OwnerId,
                DepartmentId,
                EntityId,
                EntityType,
                From,
                To,
                Bucket,
                Buckets);
    }
}
