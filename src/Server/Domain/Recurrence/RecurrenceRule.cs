namespace Everdue.Server.Domain.Recurrence;

/// <summary>
/// The immutable, provider-free description of a recurrence. Everything the engine needs to know
/// about "when" lives here; the engine itself contains no date arithmetic.
/// </summary>
/// <param name="Kind">Daily | WeeklyOnDays | MonthlyOnDay | Yearly.</param>
/// <param name="DaysOfWeekMask">WeeklyOnDays: bit set of <see cref="DayOfWeek"/> (Sunday = bit 0).</param>
/// <param name="DayOfMonth">MonthlyOnDay / Yearly: 1-31, clamped to the month's last day.</param>
/// <param name="MonthOfYear">Yearly: 1-12.</param>
/// <param name="StartDate">Local date; the first occurrence is the first scheduled date on or after it.</param>
public sealed record RecurrenceRule(
    RecurrenceKind Kind,
    int? DaysOfWeekMask,
    int? DayOfMonth,
    int? MonthOfYear,
    DateOnly StartDate)
{
    public static int MaskFor(params DayOfWeek[] days)
    {
        var mask = 0;
        foreach (var day in days)
        {
            mask |= 1 << (int)day;
        }

        return mask;
    }

    public static IReadOnlyList<DayOfWeek> DaysFromMask(int mask)
        => Enum.GetValues<DayOfWeek>().Where(d => (mask & (1 << (int)d)) != 0).ToArray();

    /// <summary>Returns null when valid, otherwise a developer-facing reason. Used by validation and by the engine's guard rails.</summary>
    public string? Validate() => Kind switch
    {
        RecurrenceKind.Daily => null,
        RecurrenceKind.WeeklyOnDays when DaysOfWeekMask is null or <= 0 or > 0b111_1111
            => "WeeklyOnDays requires DaysOfWeekMask with at least one weekday selected.",
        RecurrenceKind.WeeklyOnDays => null,
        RecurrenceKind.MonthlyOnDay when DayOfMonth is null or < 1 or > 31
            => "MonthlyOnDay requires DayOfMonth between 1 and 31.",
        RecurrenceKind.MonthlyOnDay => null,
        RecurrenceKind.Yearly when MonthOfYear is null or < 1 or > 12
            => "Yearly requires MonthOfYear between 1 and 12.",
        RecurrenceKind.Yearly when DayOfMonth is null or < 1 or > 31
            => "Yearly requires DayOfMonth between 1 and 31.",
        // Reject dates that never exist in any year (Feb 30, Apr 31, …); Feb 29 is allowed and clamps.
        RecurrenceKind.Yearly when DayOfMonth > DateTime.DaysInMonth(2024, MonthOfYear!.Value)
            => $"Yearly day {DayOfMonth} does not exist in month {MonthOfYear}.",
        RecurrenceKind.Yearly => null,
        _ => $"Unsupported recurrence kind '{Kind}'.",
    };
}
