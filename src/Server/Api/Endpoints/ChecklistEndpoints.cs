using Common.Mediator;
using Everdue.Server.Application.Checklists;
using Everdue.Server.Application.Contracts;

namespace Everdue.Server.Api.Endpoints;

public static class ChecklistEndpoints
{
    public static IEndpointRouteBuilder MapChecklistEndpoints(this IEndpointRouteBuilder api)
    {
        var items = api.MapGroup("/workitems/{id:guid}/checklist")
            .WithTags("Checklists")
            .RequireAuthorization()
            .AllowApiKey();

        items.MapGet("/", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new ListChecklistItemsQuery(id), cancellationToken)))
            .Produces<IReadOnlyList<ChecklistItemDto>>();

        items.MapPost("/", async (Guid id, AddChecklistItemBody body, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(new AddChecklistItemCommand(id, body.Text), cancellationToken);
                return Results.Created($"/api/v1/workitems/{id}/checklist/{created.Id}", created);
            })
            .WithSummary("Adds an ad-hoc item. Always non-required — only the template decides what blocks a completion.")
            .Produces<ChecklistItemDto>(StatusCodes.Status201Created);

        items.MapPost("/{itemId:guid}/check", async (Guid id, Guid itemId, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new SetChecklistItemCheckedCommand(id, itemId, true), cancellationToken)))
            .Produces<ChecklistItemDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        items.MapPost("/{itemId:guid}/uncheck", async (Guid id, Guid itemId, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new SetChecklistItemCheckedCommand(id, itemId, false), cancellationToken)))
            .Produces<ChecklistItemDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        items.MapDelete("/{itemId:guid}", async (Guid id, Guid itemId, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteChecklistItemCommand(id, itemId), cancellationToken);
                return Results.NoContent();
            })
            .WithSummary("Ad-hoc items only. A template item is part of what the occurrence was asked to do.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        var template = api.MapGroup("/responsibilities/{id:guid}/checklist-template")
            .WithTags("Checklists")
            .RequireAuthorization()
            .AllowApiKey();

        template.MapGet("/", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new GetChecklistTemplateQuery(id), cancellationToken)))
            .Produces<IReadOnlyList<ChecklistTemplateItemDto>>();

        template.MapPut("/", async (Guid id, SaveChecklistTemplateBody body, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new SaveChecklistTemplateCommand(id, body.Items), cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("Replaces the ordered template. Existing occurrences keep the snapshot they spawned with.")
            .Produces<IReadOnlyList<ChecklistTemplateItemDto>>();

        return api;
    }
}

/// <summary>
/// Bodies exist because the route already carries the id: a command record with the id inside it would have to
/// be rewritten with <c>with</c> at every call site, which is where the two quietly disagree.
/// </summary>
public sealed record AddChecklistItemBody(string Text);

public sealed record SaveChecklistTemplateBody(IReadOnlyList<ChecklistTemplateItemInput> Items);
