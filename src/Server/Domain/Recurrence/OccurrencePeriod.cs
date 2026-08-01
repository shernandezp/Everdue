namespace Everdue.Server.Domain.Recurrence;

/// <summary>
/// The three instants an occurrence is anchored to, all derived from one civil scheduled date.
/// Between <see cref="DueDate"/> and <see cref="PeriodEnd"/> the occurrence is overdue (visible
/// pressure); at <see cref="PeriodEnd"/> it is missed and its successor spawns.
/// </summary>
public readonly record struct OccurrencePeriod(
    DateOnly ScheduledDate,
    DateTimeOffset PeriodStart,
    DateTimeOffset DueDate,
    DateTimeOffset PeriodEnd)
{
    public static OccurrencePeriod For(RecurrenceRule rule, DateOnly scheduledDate, TimeZoneInfo timeZone)
    {
        var next = RecurrenceCalculator.NextScheduledDate(rule, scheduledDate);
        return new OccurrencePeriod(
            scheduledDate,
            TenantTime.StartOfDay(scheduledDate, timeZone),
            TenantTime.EndOfDay(scheduledDate, timeZone),
            TenantTime.StartOfDay(next, timeZone));
    }
}
