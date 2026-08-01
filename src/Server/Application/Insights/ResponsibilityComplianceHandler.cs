using System.Globalization;
using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Insights;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// One responsibility's compliance: the same tally the table shows, plus the ✅/❌/⏸ strip of the
/// individual periods behind it. The strip is the point of this screen — a rate says "87%", the strip
/// says which weeks, which is what a conversation about it actually needs.
/// </summary>
public sealed class ResponsibilityComplianceHandler(
    IEverdueDbContext db,
    ITenantProvider tenants,
    IUserDirectory users,
    IClock clock,
    IOptions<InsightsOptions> options)
    : IRequestHandler<ResponsibilityComplianceQuery, ResponsibilityComplianceDto>
{
    public async Task<ResponsibilityComplianceDto> Handle(
        ResponsibilityComplianceQuery request,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var settings = options.Value;
        var timeZone = await tenants.GetTimeZoneAsync(cancellationToken);

        var filter = request.Scope() with { ResponsibilityId = request.ResponsibilityId };
        var window = request.Window(BucketKind.Week, timeZone, now, settings);

        var labels = await new ResponsibilityLabelReader(db).ForAsync([request.ResponsibilityId], cancellationToken);

        if (!labels.TryGetValue(request.ResponsibilityId, out var label))
        {
            throw new NotFoundException(ResourceNames.Responsibility, request.ResponsibilityId);
        }

        var occurrences = (await new OccurrenceLedgerReader(db).InWindowAsync(filter, window, cancellationToken))
            .OrderBy(o => o.PeriodStart)
            .ToArray();

        var result = ComplianceCalculator.Build(occurrences, window, now);
        var directory = await users.MapAsync([label.OwnerUserId], cancellationToken);

        var strip = occurrences
            .Select(o => new StripPointDto(
                o.Id,
                PeriodLabel(o, timeZone),
                TenantTime.LocalDate(o.PeriodStart, timeZone),
                o.Status,
                o.HoldReason,
                o.IsConcludedAt(now)))
            .ToArray();

        return new ResponsibilityComplianceDto(
            label.Id,
            label.Title,
            InsightsRows.DisplayName(directory, label.OwnerUserId),
            InsightsRows.Compliance(label, result, directory, filter, window, now, settings.MinOccurrencesForRate),
            result.Trend,
            strip);
    }

    /// <summary>
    /// A chip is labelled by the length of its own period, not by the chart's bucket: a weekly
    /// obligation reads "W29" whether the trend beside it is drawn in weeks or in months.
    ///
    /// Daily work is labelled by date rather than by week, because a week's worth of daily chips all
    /// carrying "W31" identifies nothing — which is the whole job of a label on a strip.
    /// </summary>
    private static string PeriodLabel(LedgerOccurrence occurrence, TimeZoneInfo timeZone)
    {
        var start = TenantTime.LocalDate(occurrence.PeriodStart, timeZone);
        var days = (occurrence.PeriodEnd - occurrence.PeriodStart).TotalDays;

        return days switch
        {
            < 2 => start.ToString("MM-dd", CultureInfo.InvariantCulture),
            <= 8 => PeriodBucket.For(BucketKind.Week, start).Label,
            <= 40 => PeriodBucket.For(BucketKind.Month, start).Label,
            _ => start.Year.ToString(CultureInfo.InvariantCulture),
        };
    }
}
