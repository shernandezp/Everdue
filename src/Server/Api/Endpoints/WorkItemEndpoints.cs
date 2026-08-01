using Common.Mediator;
using Everdue.Server.Application.Comments;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.WorkItems;
using Everdue.Server.Domain;

namespace Everdue.Server.Api.Endpoints;

public sealed record HoldRequest(HoldReason? Reason, string? Text);

public sealed record RescheduleRequest(DateTimeOffset NewDueDate, string? Note);

public static class WorkItemEndpoints
{
    public static IEndpointRouteBuilder MapWorkItemEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/workitems").WithTags("Work items").RequireAuthorization().AllowApiKey();

        group.MapGet("/", async ([AsParameters] ListWorkItemsQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("The board and list views, and the target of every report drill-through.")
            .Produces<PagedResult<WorkItemDto>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new GetWorkItemQuery(id), cancellationToken)))
            .WithSummary("The item with its event history, comments and the transitions currently allowed.")
            .Produces<WorkItemDetailDto>();

        group.MapPost("/", async (CreateWorkItemCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(command, cancellationToken);
                return Results.Created($"/api/v1/workitems/{created.Id}", created);
            })
            .WithSummary("Creates a one-off task. Occurrences are engine-created and cannot be posted.")
            .Produces<WorkItemDto>(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}", async (Guid id, UpdateWorkItemCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command with { Id = id }, cancellationToken)))
            .WithSummary("Descriptive fields only. Status is never a PATCH — the rules live server-side.")
            .Produces<WorkItemDto>();

        // Transitions are explicit actions, one per legal move, so the transition matrix is enforced
        // in exactly one place instead of being re-derived from a status field the client sends.
        group.MapPost("/{id:guid}/start", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new StartWorkItemCommand(id), cancellationToken)))
            .WithSummary("Marks the item as being worked on. Does not affect whether its period is missed.")
            .Produces<WorkItemDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/complete", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new CompleteWorkItemCommand(id), cancellationToken)))
            .WithSummary("Completes the item. A missed item completes as CompletedLate; the miss stands.")
            .Produces<WorkItemDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/hold", async (Guid id, HoldRequest body, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new HoldWorkItemCommand(id, body.Reason, body.Text), cancellationToken)))
            .WithSummary("Puts the item on hold. The reason is mandatory; free text is mandatory when the reason is Other.")
            .Produces<WorkItemDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reopen", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new ReopenWorkItemCommand(id), cancellationToken)))
            .WithSummary("Releases a hold, or undoes a completion (owner or administrator).")
            .Produces<WorkItemDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new CancelWorkItemCommand(id), cancellationToken)))
            .WithSummary("Cancels a one-off task that no longer applies. Occurrences can never be cancelled.")
            .Produces<WorkItemDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reschedule", async (Guid id, RescheduleRequest body, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new RescheduleWorkItemCommand(id, body.NewDueDate, body.Note), cancellationToken)))
            .WithSummary("Moves the due date. An occurrence may only move inside its own period.")
            .Produces<WorkItemDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/bulk", async (BulkWorkItemCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command, cancellationToken)))
            .WithSummary("Runs one action over up to 100 items through the existing single-item commands, reporting each result.")
            .Produces<BulkResultDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}/comments", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new ListCommentsQuery(id), cancellationToken)))
            .Produces<IReadOnlyList<CommentDto>>();

        group.MapPost("/{id:guid}/comments", async (Guid id, AddCommentCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(command with { WorkItemId = id }, cancellationToken);
                return Results.Created($"/api/v1/workitems/{id}/comments/{created.Id}", created);
            })
            .Produces<CommentDto>(StatusCodes.Status201Created);

        return api;
    }
}

public static class CommentEndpoints
{
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapDelete("/comments/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteCommentCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithTags("Comments")
            .RequireAuthorization()
            .AllowApiKey()
            .WithSummary("Deletes a comment. Author or administrator only; there is no edit.");

        return api;
    }
}
