using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Reports;

namespace Everdue.Server.Api.Endpoints;

public static class ReportEndpoints
{
    /// <summary>
    /// Five fixed views, computed server-side. No report builder, no charts — and every number
    /// carries the work-item filter that produces its rows.
    /// </summary>
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder api)
    {
        // Reports are the manager surface; in v1 that means administrators only (revisit with usage).
        var group = api.MapGroup("/reports").WithTags("Reports").RequireAuthorization(ApiPolicies.Admin).AllowApiKey();

        group.MapGet("/exceptions", async ([AsParameters] ExceptionsReportQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Due today / completed today / overdue / missed in range / on hold by reason.")
            .Produces<ExceptionsReportDto>();

        group.MapGet("/entity-health", async ([AsParameters] EntityHealthQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Per entity: open, overdue, missed 30/60/90d, on hold, days since last activity. Sortable server-side.")
            .Produces<PagedResult<EntityHealthRowDto>>();

        group.MapGet("/neglect", async ([AsParameters] NeglectReportQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Entities with no completed activity in N days (default 90), oldest first.")
            .Produces<IReadOnlyList<NeglectRowDto>>();

        group.MapGet("/blocked-by-entity", async ([AsParameters] BlockedByEntityQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("On-hold work grouped by entity and reason, with the oldest hold in each group.")
            .Produces<IReadOnlyList<BlockedByEntityGroupDto>>();

        // The one exception to admin-only reporting: the entity drilldown is an *entity* screen, and
        // members are meant to have those. It shows one entity's own work — nothing a member cannot
        // already read from /workitems?entityId=…, so gating it would only break the link, not
        // protect anything.
        api.MapGet("/reports/entities/{id:guid}/timeline", async (
                Guid id,
                DateTimeOffset? from,
                DateTimeOffset? to,
                Guid? ownerId,
                Guid? departmentId,
                ISender sender,
                CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new EntityTimelineQuery(id, from, to, ownerId, departmentId), cancellationToken)))
            .WithTags("Reports")
            .RequireAuthorization()
            .AllowApiKey()
            .WithSummary("The entity's full occurrence timeline interleaved with its one-off work.")
            .Produces<EntityTimelineDto>();

        return api;
    }
}
