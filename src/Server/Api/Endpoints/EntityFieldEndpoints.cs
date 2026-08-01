using Common.Mediator;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Entities;

namespace Everdue.Server.Api.Endpoints;

public static class EntityFieldEndpoints
{
    /// <summary>
    /// Custom field <em>definitions</em>. Deliberately not reachable with an API key: the shape of an entity is a
    /// configuration decision, and a programmatic caller has no business changing it.
    /// </summary>
    public static IEndpointRouteBuilder MapEntityFieldEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/entity-fields")
            .WithTags("Entity fields")
            .RequireAuthorization(ApiPolicies.Admin);

        group.MapGet("/", async ([AsParameters] ListEntityFieldDefsQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Display-only reference fields. Never filterable, sortable or reportable — see the guardrails.")
            .Produces<IReadOnlyList<EntityFieldDefDto>>();

        group.MapPost("/", async (CreateEntityFieldDefCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(command, cancellationToken);
                return Results.Created($"/api/v1/entity-fields/{created.Id}", created);
            })
            .Produces<EntityFieldDefDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", async (Guid id, UpdateEntityFieldDefCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command with { Id = id }, cancellationToken)))
            .WithSummary("The field type is not editable: changing it would leave every stored value invalid.")
            .Produces<EntityFieldDefDto>();

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteEntityFieldDefCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithSummary("Values stored under it are ignored on read and dropped on the entity's next save.");

        return api;
    }
}
