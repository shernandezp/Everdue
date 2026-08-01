using Common.Mediator;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.SavedViews;

namespace Everdue.Server.Api.Endpoints;

public sealed record SaveViewRequest(string Name, string Route, string QueryString);

public static class SavedViewEndpoints
{
    public static IEndpointRouteBuilder MapSavedViewEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/saved-views").WithTags("Saved views").RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new ListSavedViewsQuery(), cancellationToken)))
            .WithSummary("Personal only — shared views are a permissions question, and permissions are v3.")
            .Produces<IReadOnlyList<SavedViewDto>>();

        group.MapPost("/", async (SaveViewRequest body, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(
                    new SaveSavedViewCommand(body.Name, body.Route, body.QueryString),
                    cancellationToken)))
            .WithSummary("Saving the same name again replaces it.")
            .Produces<SavedViewDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteSavedViewCommand(id), cancellationToken);
                return Results.NoContent();
            });

        return api;
    }
}
