using System.Collections.Concurrent;

namespace Everdue.Server.Domain;

/// <summary>
/// Resolves the tenant's IANA time zone id on any host. .NET accepts IANA ids directly on
/// Windows too (ICU), but we keep the Windows-id fallback so a machine with the legacy NLS
/// backend, or a tenant configured with a Windows id, still starts.
/// </summary>
public static class TimeZoneLookup
{
    private static readonly ConcurrentDictionary<string, TimeZoneInfo> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        return Cache.GetOrAdd(timeZoneId, static id =>
        {
            if (TryFind(id, out var tz))
            {
                return tz;
            }

            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId) && TryFind(windowsId!, out tz))
            {
                return tz;
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId) && TryFind(ianaId!, out tz))
            {
                return tz;
            }

            throw new TimeZoneNotFoundException($"Unknown time zone id '{id}'. Use an IANA identifier such as 'America/Bogota'.");
        });
    }

    public static bool IsKnown(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            Resolve(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
    }

    private static bool TryFind(string id, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }
}
