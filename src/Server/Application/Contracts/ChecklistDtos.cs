namespace Everdue.Server.Application.Contracts;

/// <summary>One line of a responsibility's template.</summary>
public sealed record ChecklistTemplateItemDto(Guid Id, string Text, bool Required, int Position);

/// <summary>One line on a work item, with who ticked it and when.</summary>
public sealed record ChecklistItemDto(
    Guid Id,
    Guid WorkItemId,
    string Text,
    bool Required,
    int Position,
    DateTimeOffset? CheckedAt,
    Guid? CheckedByUserId,
    string? CheckedByDisplayName);

/// <summary>
/// Why a completion would be refused, if it would be — so the drawer can disable the button with a
/// reason instead of letting somebody discover the rule by being turned away.
///
/// Null on any item with no completion rules at all, which is most of them.
/// </summary>
public sealed record CompletionRequirementsDto(
    int RequiredChecklistOpen,
    bool AttachmentRequired,
    int AttachmentCount)
{
    public bool Blocked => RequiredChecklistOpen > 0 || (AttachmentRequired && AttachmentCount == 0);
}
