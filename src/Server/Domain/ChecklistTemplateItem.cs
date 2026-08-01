namespace Everdue.Server.Domain;

/// <summary>
/// One line of a responsibility's checklist template.
///
/// There is deliberately no <c>ChecklistTemplates</c> header table: it would hold exactly one row per
/// responsibility, carry no field the responsibility does not already have, and add a join to every
/// read. A template <em>is</em> the responsibility's ordered items.
/// </summary>
public class ChecklistTemplateItem : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    public Guid ResponsibilityId { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Whether this item must be checked before the occurrence can be completed — and only when the
    /// responsibility's <see cref="Responsibility.RequireChecklistToComplete"/> is on. The flag on the
    /// item says "this one matters"; the flag on the responsibility says "enforce it".
    /// </summary>
    public bool Required { get; set; }

    public int Position { get; set; }

    public Responsibility? Responsibility { get; set; }
}
