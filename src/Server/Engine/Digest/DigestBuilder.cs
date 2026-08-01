using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Reports;
using Everdue.Server.Application.WorkItems;
using Everdue.Server.Domain;
using Everdue.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Engine.Digest;

/// <summary>
/// Reads the digest's sections. Pure query work, kept apart from the timing and the delivery.
///
/// The on-hold aging and neglect sections are **not new queries**: they are the existing
/// report handlers, dispatched through the mediator. A number in the digest and the same number on
/// the dashboard cannot drift apart if there is only one query behind both.
/// </summary>
public sealed class DigestBuilder(EverdueDbContext db, IUserDirectory users, ISender sender)
{
    /// <summary>Entities with no completed activity in this many days are worth naming in a digest.</summary>
    private const int NeglectDays = 30;

    private const int MaxRowsPerSection = 10;

    public async Task<DigestContent> BuildAsync(
        Tenant tenant,
        DateTimeOffset now,
        DigestFrequency frequency,
        Guid? departmentId,
        CancellationToken cancellationToken)
    {
        var timeZone = tenant.ResolveTimeZone();
        var today = TenantTime.LocalDate(now, timeZone);
        var dayStart = TenantTime.StartOfDay(today, timeZone);
        var dayEnd = TenantTime.StartOfDay(today.AddDays(1), timeZone);

        // A weekly digest covers the week's misses; "due today" always means today, whatever the
        // cadence — a list of things due four days ago is history, not a to-do list.
        var since = now.AddDays(frequency == DigestFrequency.Weekly ? -7 : -1);

        // "Went missed" is anchored to the period boundary that produced the miss, so the digest says
        // the same thing whether the engine flipped it at 00:00 or caught up at 06:55.
        var wentMissed = await Rows(Scoped(departmentId)
            .Where(w => w.Status == WorkItemStatus.Missed
                        && w.PeriodEnd != null
                        && w.PeriodEnd > since
                        && w.PeriodEnd <= now))
            .ToListAsync(cancellationToken);

        var dueToday = await Rows(Scoped(departmentId)
            .Where(w => WorkItemQueries.Outstanding.Contains(w.Status)
                        && w.DueDate >= dayStart
                        && w.DueDate < dayEnd))
            .ToListAsync(cancellationToken);

        var onHold = await Scoped(departmentId)
            .Where(w => w.Status == WorkItemStatus.OnHold && w.HoldReason != null)
            .GroupBy(w => w.HoldReason!.Value)
            .Select(g => new DigestHoldGroup(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var directory = await users.MapAsync(
            wentMissed.Select(r => r.OwnerUserId).Concat(dueToday.Select(r => r.OwnerUserId)),
            cancellationToken);

        return new DigestContent(
            tenant.Name,
            today,
            timeZone,
            frequency,
            departmentId is null ? null : await DepartmentNameAsync(departmentId.Value, cancellationToken),
            wentMissed.Select(r => ToItem(r, directory)).OrderBy(i => i.DueDate).ToArray(),
            dueToday.Select(r => ToItem(r, directory)).OrderBy(i => i.DueDate).ToArray(),
            onHold,
            await AgingAsync(departmentId, now, cancellationToken),
            await NeglectAsync(departmentId, cancellationToken));
    }

    /// <summary>On-hold work grouped by entity and reason — the existing blocked-by-entity report, trimmed.</summary>
    private async Task<IReadOnlyList<DigestAgingRow>> AgingAsync(
        Guid? departmentId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var groups = await sender.Send(new BlockedByEntityQuery(DepartmentId: departmentId), cancellationToken);

        return groups
            .SelectMany(group => group.Reasons.Select(reason => new DigestAgingRow(
                group.EntityName,
                reason.Reason,
                reason.Count,
                reason.OldestHoldAt is { } oldest ? Math.Max(0, (int)(now - oldest).TotalDays) : 0)))
            .OrderByDescending(row => row.OldestDays)
            .Take(MaxRowsPerSection)
            .ToArray();
    }

    /// <summary>Entities nobody has completed anything for — the existing neglect report, trimmed.</summary>
    private async Task<IReadOnlyList<DigestNeglectRow>> NeglectAsync(Guid? departmentId, CancellationToken cancellationToken)
    {
        var rows = await sender.Send(new NeglectReportQuery(NeglectDays, DepartmentId: departmentId), cancellationToken);

        return rows
            .Take(MaxRowsPerSection)
            .Select(row => new DigestNeglectRow(row.EntityName, row.DaysSinceLastActivity, row.OpenCount))
            .ToArray();
    }

    private IQueryable<WorkItem> Scoped(Guid? departmentId)
    {
        var query = db.WorkItems.AsNoTracking();
        return departmentId is { } department ? query.Where(w => w.DepartmentId == department) : query;
    }

    private Task<string?> DepartmentNameAsync(Guid departmentId, CancellationToken cancellationToken)
        => db.Departments.AsNoTracking()
            .Where(d => d.Id == departmentId)
            .Select(d => (string?)d.Name)
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record DigestRow(string Title, string? EntityName, Guid OwnerUserId, DateTimeOffset DueDate);

    private static IQueryable<DigestRow> Rows(IQueryable<WorkItem> query)
        => query.Select(w => new DigestRow(
            w.Title,
            w.EntityId == null ? null : w.Entity!.Name,
            w.OwnerUserId,
            w.DueDate));

    private static DigestItem ToItem(DigestRow row, IReadOnlyDictionary<Guid, UserSummary> directory)
        => new(
            row.Title,
            row.EntityName,
            directory.TryGetValue(row.OwnerUserId, out var owner) ? owner.DisplayName : "—",
            row.DueDate);
}
