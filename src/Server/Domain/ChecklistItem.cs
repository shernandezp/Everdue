namespace Everdue.Server.Domain;

/// <summary>
/// A checklist line on one work item — a <em>snapshot</em>, copied from the responsibility's template
/// when the engine spawns the occurrence. Editing the template afterwards never rewrites history,
/// which is the whole reason this is a copy and not a foreign key.
///
/// Checking a line writes no <see cref="WorkItemEvent"/>: <see cref="CheckedAt"/> and
/// <see cref="CheckedByUserId"/> <em>are</em> the record of who ticked what and when, and fifteen
/// events per inspection would bury the status history the timeline exists for.
/// </summary>
public class ChecklistItem : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    public Guid WorkItemId { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Copied from the template. Ad-hoc items added on the item itself are always false, so a
    /// completion gate can never be invented mid-period on somebody else's work.
    /// </summary>
    public bool Required { get; set; }

    public int Position { get; set; }

    public DateTimeOffset? CheckedAt { get; set; }

    public Guid? CheckedByUserId { get; set; }

    public WorkItem? WorkItem { get; set; }

    public bool IsChecked => CheckedAt is not null;

    /// <summary>Whether this line stands in the way of a completion.</summary>
    public bool BlocksCompletion => Required && !IsChecked;

    /// <summary>
    /// Copies a template line onto a freshly spawned occurrence. Kept here rather than in the engine
    /// so the two places that create checklist items (spawn and ad-hoc) cannot drift.
    /// </summary>
    public static ChecklistItem FromTemplate(ChecklistTemplateItem template, WorkItem item)
        => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = item.TenantId,
            WorkItemId = item.Id,
            Text = template.Text,
            Required = template.Required,
            Position = template.Position,
        };
}
