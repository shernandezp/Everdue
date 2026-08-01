using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Checklists;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.WorkItems;

/// <summary>
/// The one gate between "somebody clicked complete" and a completion.
///
/// Both rules live here and nowhere else, which is what makes them impossible to bypass:
/// <c>CompleteWorkItemHandler</c> calls it, and the bulk endpoint gets it for free because it dispatches
/// the same command. The rules apply to <strong>occurrences only</strong> — a one-off task has no
/// responsibility and therefore no rule — and only from the next completion attempt: nothing already
/// completed is reopened when a flag is switched on.
/// </summary>
public sealed class CompletionPreconditions(IEverdueDbContext db, ChecklistProgressReader checklists)
{
    /// <summary>
    /// What stands in the way, or null when nothing does. Read by the item detail endpoint so the UI can
    /// disable the button with a reason — the refusal below is the enforcement, this is the courtesy.
    /// </summary>
    public Task<CompletionRequirementsDto?> DescribeAsync(WorkItem item, CancellationToken cancellationToken)
        => DescribeAsync(item.Id, item.ResponsibilityId, cancellationToken);

    /// <summary>
    /// The id pair overload exists so a caller that already has a projected row does not have to load the entity
    /// again purely to describe its requirements — which is the whole of what this needs.
    /// </summary>
    public async Task<CompletionRequirementsDto?> DescribeAsync(
        Guid workItemId,
        Guid? responsibilityId,
        CancellationToken cancellationToken)
    {
        if (responsibilityId is not { } responsibility)
        {
            return null;
        }

        var rules = await db.Responsibilities.AsNoTracking()
            .Where(r => r.Id == responsibility)
            .Select(r => new { r.RequireChecklistToComplete, r.RequireAttachmentToComplete })
            .FirstOrDefaultAsync(cancellationToken);

        if (rules is null || (!rules.RequireChecklistToComplete && !rules.RequireAttachmentToComplete))
        {
            return null;
        }

        var requiredOpen = 0;
        if (rules.RequireChecklistToComplete)
        {
            var progress = await checklists.ForOneAsync(workItemId, cancellationToken);
            requiredOpen = progress?.RequiredOpen ?? 0;
        }

        var attachments = rules.RequireAttachmentToComplete
            ? await db.Attachments.CountAsync(a => a.WorkItemId == workItemId, cancellationToken)
            : 0;

        return new CompletionRequirementsDto(requiredOpen, rules.RequireAttachmentToComplete, attachments);
    }

    /// <summary>
    /// Refuses with <see cref="ConflictException"/> — 409, the same status the board already renders when
    /// it turns a drag down, so the reason reaches the user through plumbing that exists.
    /// </summary>
    public async Task EnsureCompletableAsync(WorkItem item, CancellationToken cancellationToken)
    {
        var requirements = await DescribeAsync(item, cancellationToken);

        if (requirements is null || !requirements.Blocked)
        {
            return;
        }

        var reasons = new List<string>(2);

        if (requirements.RequiredChecklistOpen > 0)
        {
            reasons.Add(requirements.RequiredChecklistOpen == 1
                ? "1 required checklist item is still unchecked"
                : $"{requirements.RequiredChecklistOpen} required checklist items are still unchecked");
        }

        if (requirements.AttachmentRequired && requirements.AttachmentCount == 0)
        {
            reasons.Add("this responsibility requires a photo or file as proof of completion");
        }

        throw new ConflictException($"This occurrence cannot be completed yet: {string.Join("; ", reasons)}.");
    }
}
