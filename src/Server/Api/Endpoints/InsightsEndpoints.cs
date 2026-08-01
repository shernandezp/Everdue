using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Insights;

namespace Everdue.Server.Api.Endpoints;

public static class InsightsEndpoints
{
    /// <summary>
    /// The intelligence layer: six computed views over the occurrence ledger, no new data entry and no
    /// precomputation behind any of them.
    ///
    /// Administrators only — and not merely by convention. Reliability per person is management
    /// information for deciding where to help, and exposing it to peers would turn it into the
    /// leaderboard the product explicitly rejects.
    /// </summary>
    public static IEndpointRouteBuilder MapInsightsEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/insights").WithTags("Insights").RequireAuthorization(ApiPolicies.Admin).AllowApiKey();

        group.MapGet("/compliance", async ([AsParameters] ComplianceQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Compliance rate per responsibility, with the trend behind it. Sorted by misses first.")
            .Produces<PagedResult<ComplianceRowDto>>();

        group.MapGet("/responsibilities/{responsibilityId:guid}/compliance", async (
                    [AsParameters] ResponsibilityComplianceQuery query,
                    ISender sender,
                    CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("One responsibility: the same tally, its bucket series, and the ✅/❌/⏸ strip of its periods.")
            .Produces<ResponsibilityComplianceDto>();

        group.MapGet("/reliability", async ([AsParameters] ReliabilityQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Per person, over occurrences only, with external waits shown beside the rate.")
            .Produces<IReadOnlyList<ReliabilityRowDto>>();

        group.MapGet("/concentration", async ([AsParameters] ConcentrationQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Completed work per entity per bucket — counts of work items, never hours.")
            .Produces<ConcentrationSeriesDto>();

        group.MapGet("/hold-aging", async ([AsParameters] HoldAgingQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Calendar days spent on hold, per reason and per entity, rebuilt from the event log.")
            .Produces<HoldAgingDto>();

        group.MapGet("/chronic", async ([AsParameters] ChronicDelayQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Responsibilities with K misses in their last N periods. Feeds the dashboard block.")
            .Produces<IReadOnlyList<ChronicResponsibilityDto>>();

        return api;
    }
}
