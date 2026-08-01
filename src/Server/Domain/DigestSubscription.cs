namespace Everdue.Server.Domain;

/// <summary>
/// Who gets the manager digest, how often, and about which department.
///
/// A row is created lazily — when a user first customises their digest, or when one is first sent to
/// them. An active administrator with no row is treated as a daily, org-wide subscriber, which is
/// exactly the original default behavior, so an upgraded install keeps sending the same digest with no admin action
/// and no data migration. Unsubscribing is a row with <see cref="Active"/> false.
/// </summary>
public class DigestSubscription : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public DigestFrequency Frequency { get; set; } = DigestFrequency.Daily;

    /// <summary>Only meaningful for <see cref="DigestFrequency.Weekly"/>. Defaults to Monday.</summary>
    public DayOfWeek WeeklyDayOfWeek { get; set; } = DayOfWeek.Monday;

    /// <summary>Null = the whole organisation.</summary>
    public Guid? DepartmentId { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>Restart-safe "already sent today" guard, per subscriber.</summary>
    public DateOnly? LastSentLocalDate { get; set; }

    public Department? Department { get; set; }

    /// <summary>Is this subscription due on the given local date?</summary>
    public bool IsDueOn(DateOnly localDate)
        => Active
           && LastSentLocalDate != localDate
           && (Frequency == DigestFrequency.Daily || localDate.DayOfWeek == WeeklyDayOfWeek);
}
