using Everdue.Server.Domain.Recurrence;

namespace Everdue.Server.Domain;

/// <summary>
/// A permanent obligation. It never "finishes" — every period the engine spawns an occurrence,
/// regardless of whether the previous one was completed.
/// </summary>
public class Responsibility : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid OwnerUserId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? EntityId { get; set; }

    public RecurrenceKind RecurrenceKind { get; set; }

    /// <summary>Bit set of <see cref="DayOfWeek"/> values (Sunday = bit 0). Used by WeeklyOnDays.</summary>
    public int? DaysOfWeekMask { get; set; }

    /// <summary>1-31 for MonthlyOnDay; 1-31 (day component) for Yearly. Clamped to the month length.</summary>
    public int? DayOfMonth { get; set; }

    /// <summary>1-12; the month component of the Yearly rule, stored as two columns rather than one combined value.</summary>
    public int? MonthOfYear { get; set; }

    /// <summary>Local (tenant-timezone) date. The first occurrence is the first scheduled date on or after this.</summary>
    public DateOnly StartDate { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>
    /// When set, an occurrence of this responsibility cannot be completed while a checklist item marked
    /// <see cref="ChecklistTemplateItem.Required"/> is unchecked. Server-enforced — this is the rule
    /// that makes the inspection/SOP use case real rather than advisory.
    /// </summary>
    public bool RequireChecklistToComplete { get; set; }

    /// <summary>
    /// When set, an occurrence cannot be completed without at least one attachment: the photo or file
    /// that proves the work happened. Reuses the existing attachments feature — it adds a rule, not a feature.
    /// </summary>
    public bool RequireAttachmentToComplete { get; set; }

    /// <summary>
    /// Either completion rule is switched on for future completions only. Nothing already completed is
    /// reopened, and an open occurrence is not retroactively blocked until somebody tries to finish it.
    /// </summary>
    public bool HasCompletionRules => RequireChecklistToComplete || RequireAttachmentToComplete;

    /// <summary>
    /// When set in the future, no occurrences spawn. Periods that end on or before this date are
    /// skipped on resume — a sanctioned pause is not a miss.
    /// </summary>
    public DateTimeOffset? PausedUntil { get; set; }

    public Department? Department { get; set; }

    public Entity? Entity { get; set; }

    public RecurrenceRule ToRule() => new(RecurrenceKind, DaysOfWeekMask, DayOfMonth, MonthOfYear, StartDate);

    public bool IsPausedAt(DateTimeOffset instant) => PausedUntil is { } until && until > instant;

    public bool SpawnsAt(DateTimeOffset instant) => Active && !IsPausedAt(instant);
}
