namespace Everdue.Server.Domain;

/// <summary>
/// Civil-local ↔ UTC conversion for a tenant. Everything is stored UTC; every period and due
/// boundary is computed as a civil local date first and only then converted, so DST never
/// shifts a period boundary off midnight.
/// </summary>
public static class TenantTime
{
    /// <summary>Local 00:00 of <paramref name="date"/>, as a UTC instant. This is a PeriodStart / PeriodEnd.</summary>
    public static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo tz)
        => ToInstant(date.ToDateTime(TimeOnly.MinValue), tz);

    /// <summary>Local 23:59:59 of <paramref name="date"/>, as a UTC instant. This is a DueDate.</summary>
    public static DateTimeOffset EndOfDay(DateOnly date, TimeZoneInfo tz)
        => ToInstant(date.ToDateTime(new TimeOnly(23, 59, 59)), tz);

    /// <summary>Local hour boundary (e.g. the digest hour) of <paramref name="date"/>, as a UTC instant.</summary>
    public static DateTimeOffset AtHour(DateOnly date, int hourLocal, TimeZoneInfo tz)
        => ToInstant(date.ToDateTime(new TimeOnly(Math.Clamp(hourLocal, 0, 23), 0)), tz);

    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo tz)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, tz).DateTime);

    public static DateTime LocalDateTime(DateTimeOffset instant, TimeZoneInfo tz)
        => TimeZoneInfo.ConvertTime(instant, tz).DateTime;

    /// <summary>
    /// Converts a civil local date/time to the UTC instant it denotes.
    /// Spring-forward gaps (a local midnight that does not exist, as in Chile or Cuba) resolve to
    /// the earliest valid instant; fall-back ambiguity resolves to the first of the two passes.
    /// </summary>
    public static DateTimeOffset ToInstant(DateTime civil, TimeZoneInfo tz)
    {
        var local = DateTime.SpecifyKind(civil, DateTimeKind.Unspecified);

        if (tz.IsInvalidTime(local))
        {
            // Walk forward to the first existing local time — that is the instant the clock jumped to.
            for (var minutes = 1; minutes <= 24 * 60; minutes++)
            {
                var candidate = local.AddMinutes(minutes);
                if (!tz.IsInvalidTime(candidate))
                {
                    local = candidate;
                    break;
                }
            }
        }

        if (tz.IsAmbiguousTime(local))
        {
            // The larger offset is the earlier of the two instants (still on DST).
            var earliest = tz.GetAmbiguousTimeOffsets(local).Max();
            return new DateTimeOffset(local, earliest).ToUniversalTime();
        }

        return new DateTimeOffset(local, tz.GetUtcOffset(local)).ToUniversalTime();
    }
}
