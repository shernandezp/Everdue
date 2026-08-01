namespace Everdue.Server.Domain.Recurrence;

/// <summary>
/// The most-tested code in the repository: a pure function over civil local dates. No time zones,
/// no UTC, no database. Callers convert the returned dates to instants via <see cref="TenantTime"/>.
/// </summary>
public static class RecurrenceCalculator
{
    /// <summary>True when <paramref name="date"/> is one of the rule's scheduled dates (ignoring StartDate).</summary>
    public static bool IsScheduled(RecurrenceRule rule, DateOnly date) => rule.Kind switch
    {
        RecurrenceKind.Daily => true,
        RecurrenceKind.WeeklyOnDays => (rule.DaysOfWeekMask!.Value & (1 << (int)date.DayOfWeek)) != 0,
        RecurrenceKind.MonthlyOnDay => date.Day == ClampDay(rule.DayOfMonth!.Value, date.Year, date.Month),
        RecurrenceKind.Yearly => date.Month == rule.MonthOfYear!.Value
                                 && date.Day == ClampDay(rule.DayOfMonth!.Value, date.Year, rule.MonthOfYear!.Value),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Kind, "Unsupported recurrence kind."),
    };

    /// <summary>The first scheduled date strictly after <paramref name="afterLocalDate"/>.</summary>
    public static DateOnly NextScheduledDate(RecurrenceRule rule, DateOnly afterLocalDate)
    {
        Guard(rule);

        return rule.Kind switch
        {
            RecurrenceKind.Daily => afterLocalDate.AddDays(1),
            RecurrenceKind.WeeklyOnDays => NextWeekly(rule.DaysOfWeekMask!.Value, afterLocalDate),
            RecurrenceKind.MonthlyOnDay => NextMonthly(rule.DayOfMonth!.Value, afterLocalDate),
            RecurrenceKind.Yearly => NextYearly(rule.MonthOfYear!.Value, rule.DayOfMonth!.Value, afterLocalDate),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Kind, "Unsupported recurrence kind."),
        };
    }

    /// <summary>The first scheduled date on or after <paramref name="date"/>.</summary>
    public static DateOnly FirstScheduledOnOrAfter(RecurrenceRule rule, DateOnly date)
    {
        Guard(rule);
        return IsScheduled(rule, date) ? date : NextScheduledDate(rule, date);
    }

    /// <summary>The rule's very first occurrence date: the first scheduled date on or after StartDate.</summary>
    public static DateOnly FirstScheduledDate(RecurrenceRule rule)
        => FirstScheduledOnOrAfter(rule, rule.StartDate);

    /// <summary>Enumerates scheduled dates strictly after <paramref name="afterLocalDate"/>, lazily and forever.</summary>
    public static IEnumerable<DateOnly> ScheduledDatesAfter(RecurrenceRule rule, DateOnly afterLocalDate)
    {
        Guard(rule);
        var cursor = afterLocalDate;
        while (true)
        {
            cursor = NextScheduledDate(rule, cursor);
            yield return cursor;
        }
    }

    /// <summary>Day <paramref name="day"/> of the given month, clamped to that month's last day (31 → Feb 28/29).</summary>
    public static int ClampDay(int day, int year, int month)
        => Math.Min(day, DateTime.DaysInMonth(year, month));

    private static DateOnly NextWeekly(int mask, DateOnly after)
    {
        for (var offset = 1; offset <= 7; offset++)
        {
            var candidate = after.AddDays(offset);
            if ((mask & (1 << (int)candidate.DayOfWeek)) != 0)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("WeeklyOnDays mask selects no weekday.");
    }

    private static DateOnly NextMonthly(int dayOfMonth, DateOnly after)
    {
        // At most two steps are ever needed, but a small bound keeps the loop obviously terminating.
        var anchor = new DateOnly(after.Year, after.Month, 1);
        for (var step = 0; step <= 2; step++)
        {
            var month = anchor.AddMonths(step);
            var candidate = new DateOnly(month.Year, month.Month, ClampDay(dayOfMonth, month.Year, month.Month));
            if (candidate > after)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Could not find a monthly date after {after:O}.");
    }

    private static DateOnly NextYearly(int month, int dayOfMonth, DateOnly after)
    {
        for (var step = 0; step <= 2; step++)
        {
            var year = after.Year + step;
            var candidate = new DateOnly(year, month, ClampDay(dayOfMonth, year, month));
            if (candidate > after)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Could not find a yearly date after {after:O}.");
    }

    private static void Guard(RecurrenceRule rule)
    {
        if (rule.Validate() is { } problem)
        {
            throw new ArgumentException(problem, nameof(rule));
        }
    }
}
