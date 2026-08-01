using System.Text.Json;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Insights;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// Rebuilds every stretch of hold time from the append-only event log — which is why that log shipped
/// in v1, and why this works retroactively over history nobody recorded for reporting.
/// </summary>
internal sealed class HoldIntervalReader(IEverdueDbContext db)
{
    private sealed record HoldEvent(
        Guid WorkItemId,
        DateTimeOffset Timestamp,
        WorkItemStatus? FromStatus,
        WorkItemStatus? ToStatus,
        string? DataJson);

    /// <summary>
    /// Every hold interval that had ended, or was still open, by <paramref name="to"/>.
    ///
    /// Deliberately unbounded below: a hold opened two hundred days ago is still accruing wait time
    /// inside today's window, and cutting the query off would report a fraction of it. That is
    /// affordable because entering and leaving a hold are rare events, not per-item rows.
    ///
    /// Callers clip the result to their own window.
    /// </summary>
    public async Task<IReadOnlyList<HoldInterval>> ReadAsync(
        DateTimeOffset to,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var events = await db.WorkItemEvents.AsNoTracking()

            // The EventType filter is load-bearing, not defensive: a Rescheduled event copies the
            // item's current status into BOTH FromStatus and ToStatus, so rescheduling a held item
            // would otherwise read as leaving and re-entering the hold in the same instant.
            .Where(e => e.EventType == WorkItemEventType.StatusChanged
                        && (e.ToStatus == WorkItemStatus.OnHold || e.FromStatus == WorkItemStatus.OnHold)
                        && e.Timestamp <= to)
            .OrderBy(e => e.Timestamp)
            .Select(e => new HoldEvent(e.WorkItemId, e.Timestamp, e.FromStatus, e.ToStatus, e.DataJson))
            .ToListAsync(cancellationToken);

        // An interval with no exit is still running; it accrues up to the window's end, never past now.
        var openEnd = to < now ? to : now;
        var intervals = new List<HoldInterval>();

        // Grouped in memory rather than ordered by id in SQL: GUID collation differs between the two
        // providers, and Enumerable.GroupBy preserves the timestamp order the query already applied.
        foreach (var item in events.GroupBy(e => e.WorkItemId))
        {
            HoldEvent? entry = null;

            foreach (var row in item)
            {
                if (row.ToStatus == WorkItemStatus.OnHold)
                {
                    // Two entries with no exit between them cannot happen through the transition
                    // matrix; if the log ever says otherwise, close the first rather than lose it.
                    if (entry is { } unclosed)
                    {
                        intervals.Add(Closed(unclosed, row.Timestamp));
                    }

                    entry = row;
                    continue;
                }

                // Leaving a hold: released, started, completed, or flipped to Missed by the engine —
                // every one of them is an exit, and the engine's miss is not a special case.
                if (entry is { } open)
                {
                    intervals.Add(Closed(open, row.Timestamp));
                    entry = null;
                }
            }

            if (entry is { } stillOpen && openEnd > stillOpen.Timestamp)
            {
                intervals.Add(new HoldInterval(
                    stillOpen.WorkItemId,
                    ReasonOf(stillOpen),
                    stillOpen.Timestamp,
                    openEnd,
                    Open: true));
            }
        }

        return intervals;
    }

    private static HoldInterval Closed(HoldEvent entry, DateTimeOffset end)
        => new(entry.WorkItemId, ReasonOf(entry), entry.Timestamp, end, Open: false);

    /// <summary>
    /// The reason as it was when the hold started, read from the entry event's payload.
    ///
    /// It has to come from here: the column on the work item is cleared the moment the hold is
    /// released, so a finished hold's reason exists nowhere else. Parsed in memory because a JSON
    /// predicate is not portable across the two providers — and an unreadable payload is counted as
    /// <see cref="HoldReason.Other"/> rather than allowed to fail a whole report.
    /// </summary>
    private static HoldReason ReasonOf(HoldEvent entry)
    {
        if (string.IsNullOrWhiteSpace(entry.DataJson))
        {
            return HoldReason.Other;
        }

        try
        {
            using var document = JsonDocument.Parse(entry.DataJson);

            return document.RootElement.TryGetProperty("reason", out var reason)
                   && reason.ValueKind == JsonValueKind.String
                   && Enum.TryParse<HoldReason>(reason.GetString(), ignoreCase: true, out var parsed)
                   && Enum.IsDefined(parsed)
                ? parsed
                : HoldReason.Other;
        }
        catch (JsonException)
        {
            return HoldReason.Other;
        }
    }
}
