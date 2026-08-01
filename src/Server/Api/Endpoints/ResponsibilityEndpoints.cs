using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Responsibilities;

namespace Everdue.Server.Api.Endpoints;

public sealed record PauseRequest(DateOnly Until);

public static class ResponsibilityEndpoints
{
    public static IEndpointRouteBuilder MapResponsibilityEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/responsibilities").WithTags("Responsibilities").RequireAuthorization().AllowApiKey();

        group.MapGet("/", async ([AsParameters] ListResponsibilitiesQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Lists responsibilities. Owners can read; administrators write.")
            .Produces<PagedResult<ResponsibilityDto>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new GetResponsibilityQuery(id), cancellationToken)))
            .Produces<ResponsibilityDto>();

        group.MapPost("/", async (CreateResponsibilityCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(command, cancellationToken);
                return Results.Created($"/api/v1/responsibilities/{created.Id}", created);
            })
            .RequireAuthorization(ApiPolicies.Admin)
            .Produces<ResponsibilityDto>(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}", async (Guid id, UpdateResponsibilityCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command with { Id = id }, cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .Produces<ResponsibilityDto>();

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new DeactivateResponsibilityCommand(id), cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("Deactivates the responsibility. Existing occurrences stay in the ledger.")
            .Produces<ResponsibilityDto>();

        group.MapPost("/{id:guid}/pause", async (Guid id, PauseRequest body, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new PauseResponsibilityCommand(id, body.Until), cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("Pauses spawning through the end of the given local date. Skipped periods are never missed.")
            .Produces<ResponsibilityDto>();

        group.MapPost("/{id:guid}/resume", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new ResumeResponsibilityCommand(id), cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .Produces<ResponsibilityDto>();

        group.MapPost("/{id:guid}/reassign", async (Guid id, ReassignResponsibilityRequest body, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(
                    new ReassignResponsibilityCommand(id, body.NewOwnerUserId, body.ApplyToWorkableOccurrences),
                    cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("Hands the responsibility over. Future occurrences follow automatically; existing workable ones follow on request.")
            .Produces<ReassignResultDto>();

        return api;
    }
}

public sealed record ReassignResponsibilityRequest(Guid NewOwnerUserId, bool ApplyToWorkableOccurrences);
