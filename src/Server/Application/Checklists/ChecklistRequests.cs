using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Application.Contracts;

namespace Everdue.Server.Application.Checklists;

public sealed record GetChecklistTemplateQuery(Guid ResponsibilityId) : IQuery<IReadOnlyList<ChecklistTemplateItemDto>>;

/// <summary>One line as it is submitted. Position comes from the order of the list, not from a field.</summary>
public sealed record ChecklistTemplateItemInput(
    [property: Required, MaxLength(300)] string Text,
    bool Required = false);

/// <summary>
/// Replaces the whole template. A wholesale replace rather than per-item CRUD because reordering,
/// renaming and deleting always arrive together from a form — and because a template is small enough
/// that diffing it server-side is cheaper than four endpoints.
/// </summary>
public sealed record SaveChecklistTemplateCommand(
    Guid ResponsibilityId,
    IReadOnlyList<ChecklistTemplateItemInput> Items) : ICommand<IReadOnlyList<ChecklistTemplateItemDto>>;

public sealed record ListChecklistItemsQuery(Guid WorkItemId) : IQuery<IReadOnlyList<ChecklistItemDto>>;

/// <summary>
/// An ad-hoc line on any work item — an inspector noticing something extra is ordinary work.
/// It is always created non-required, whatever the caller asks: only the template decides what blocks
/// a completion, so a gate can never be invented mid-period on somebody else's item.
/// </summary>
public sealed record AddChecklistItemCommand(
    Guid WorkItemId,
    [property: Required, MaxLength(300)] string Text) : ICommand<ChecklistItemDto>;

public sealed record SetChecklistItemCheckedCommand(Guid WorkItemId, Guid ItemId, bool Checked) : ICommand<ChecklistItemDto>;

/// <summary>Ad-hoc lines only. A template line is history and stays on the record.</summary>
public sealed record DeleteChecklistItemCommand(Guid WorkItemId, Guid ItemId) : ICommand<bool>;
