using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;

namespace Everdue.Server.Application.WorkItems;

public enum BulkWorkItemAction
{
    Complete = 0,
    Reassign = 1,
    Reschedule = 2,
}

/// <summary>
/// One round trip, per-item results.
///
/// Deliberately not a new set of rules: each id is put through the **existing single-item command**,
/// so the transition matrix is still enforced in exactly one place. What this adds over a loop in the
/// browser is that a partial failure is a described outcome rather than a half-finished list and a
/// user wondering which half.
/// </summary>
public sealed record BulkWorkItemCommand(
    IReadOnlyList<Guid> Ids,
    string Action,
    Guid? OwnerUserId = null,
    DateTimeOffset? NewDueDate = null,
    string? Note = null) : ICommand<BulkResultDto>
{
    public const int MaxItems = 100;

    public BulkWorkItemAction ResolvedAction => EnumQuery.ParseOr(Action, nameof(Action), BulkWorkItemAction.Complete);
}

public sealed class BulkWorkItemHandler(ISender sender) : IRequestHandler<BulkWorkItemCommand, BulkResultDto>
{
    public async Task<BulkResultDto> Handle(BulkWorkItemCommand request, CancellationToken cancellationToken = default)
    {
        if (request.Ids.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["ids"] = ["Select at least one item."] });
        }

        if (request.Ids.Count > BulkWorkItemCommand.MaxItems)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["ids"] = [$"At most {BulkWorkItemCommand.MaxItems} items can be changed at once."],
            });
        }

        var action = request.ResolvedAction;
        ValidateArguments(request, action);

        var succeeded = new List<Guid>();
        var failed = new List<BulkItemFailureDto>();

        foreach (var id in request.Ids.Distinct())
        {
            try
            {
                await DispatchAsync(id, request, action, cancellationToken);
                succeeded.Add(id);
            }
            catch (AppException e)
            {
                // An item that was already completed, or that somebody else just moved, is a normal
                // outcome of a bulk selection — it is reported, not allowed to abort the rest.
                failed.Add(new BulkItemFailureDto(id, e.Message));
            }
        }

        return new BulkResultDto(succeeded, failed);
    }

    private static void ValidateArguments(BulkWorkItemCommand request, BulkWorkItemAction action)
    {
        switch (action)
        {
            case BulkWorkItemAction.Reassign when request.OwnerUserId is null:
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["ownerUserId"] = ["A new owner is required to reassign."],
                });

            case BulkWorkItemAction.Reschedule when request.NewDueDate is null:
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["newDueDate"] = ["A new due date is required to reschedule."],
                });
        }
    }

    private async Task DispatchAsync(
        Guid id,
        BulkWorkItemCommand request,
        BulkWorkItemAction action,
        CancellationToken cancellationToken)
    {
        switch (action)
        {
            case BulkWorkItemAction.Complete:
                await sender.Send(new CompleteWorkItemCommand(id), cancellationToken);
                break;

            case BulkWorkItemAction.Reschedule:
                await sender.Send(new RescheduleWorkItemCommand(id, request.NewDueDate!.Value, request.Note), cancellationToken);
                break;

            case BulkWorkItemAction.Reassign:
                // Reassignment goes through the ordinary edit, which means it writes the same
                // attributed event a one-at-a-time hand-over does.
                var current = await sender.Send(new GetWorkItemQuery(id), cancellationToken);
                var item = current.Item;

                await sender.Send(
                    new UpdateWorkItemCommand(
                        id,
                        item.Title,
                        item.Description,
                        request.OwnerUserId!.Value,
                        item.EntityId,
                        item.DepartmentId),
                    cancellationToken);
                break;
        }
    }
}
