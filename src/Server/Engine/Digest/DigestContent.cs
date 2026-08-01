using Everdue.Server.Domain;

namespace Everdue.Server.Engine.Digest;

public sealed record DigestItem(string Title, string? EntityName, string OwnerName, DateTimeOffset DueDate);

public sealed record DigestHoldGroup(HoldReason Reason, int Count);

/// <summary>One entity's oldest block, so a manager can see what has been stuck longest and on whom.</summary>
public sealed record DigestAgingRow(string EntityName, HoldReason Reason, int Count, int OldestDays);

/// <summary>An entity nobody has completed anything for. Null days = never.</summary>
public sealed record DigestNeglectRow(string EntityName, int? DaysSinceLastActivity, int OpenCount);

public sealed record DigestContent(
    string TenantName,
    DateOnly LocalDate,
    TimeZoneInfo TimeZone,
    DigestFrequency Frequency,
    string? DepartmentName,
    IReadOnlyList<DigestItem> WentMissed,
    IReadOnlyList<DigestItem> DueToday,
    IReadOnlyList<DigestHoldGroup> OnHold,
    IReadOnlyList<DigestAgingRow> OnHoldAging,
    IReadOnlyList<DigestNeglectRow> Neglect)
{
    public bool IsEmpty =>
        WentMissed.Count == 0
        && DueToday.Count == 0
        && OnHold.Count == 0
        && OnHoldAging.Count == 0
        && Neglect.Count == 0;
}
