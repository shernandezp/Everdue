using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Departments;

namespace Everdue.Server.Api.Endpoints;

public static class DepartmentEndpoints
{
    public static IEndpointRouteBuilder MapDepartmentEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/departments").WithTags("Departments").RequireAuthorization().AllowApiKey();

        group.MapGet("/", async ([AsParameters] ListDepartmentsQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Lists departments — the teams that execute work, not the entities work is about.")
            .Produces<PagedResult<DepartmentDto>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new GetDepartmentQuery(id), cancellationToken)))
            .Produces<DepartmentDto>();

        group.MapPost("/", async (CreateDepartmentCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(command, cancellationToken);
                return Results.Created($"/api/v1/departments/{created.Id}", created);
            })
            .RequireAuthorization(ApiPolicies.Admin)
            .Produces<DepartmentDto>(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}", async (Guid id, UpdateDepartmentCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command with { Id = id }, cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .Produces<DepartmentDto>();

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new DeactivateDepartmentCommand(id), cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .Produces<DepartmentDto>();

        return api;
    }
}
