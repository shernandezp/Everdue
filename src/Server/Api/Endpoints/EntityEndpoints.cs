using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Entities;

namespace Everdue.Server.Api.Endpoints;

public static class EntityEndpoints
{
    public static IEndpointRouteBuilder MapEntityEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/entities").WithTags("Entities").RequireAuthorization().AllowApiKey();

        group.MapGet("/", async ([AsParameters] ListEntitiesQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Lists entities. Members read; administrators write.")
            .Produces<PagedResult<EntityDto>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new GetEntityQuery(id), cancellationToken)))
            .Produces<EntityDto>();

        group.MapPost("/", async (CreateEntityCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(command, cancellationToken);
                return Results.Created($"/api/v1/entities/{created.Id}", created);
            })
            .RequireAuthorization(ApiPolicies.Admin)
            .Produces<EntityDto>(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}", async (Guid id, UpdateEntityCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command with { Id = id }, cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .Produces<EntityDto>();

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new DeactivateEntityCommand(id), cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("Soft-deactivates the entity; the ledger keeps referring to it forever.")
            .Produces<EntityDto>();

        return api;
    }
}
