using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain.Insights;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// Compliance per responsibility: the "Week 29 ✅ Week 30 ❌" series expressed as a rate, over rows the
/// team produced by working. Nothing here is entered by hand, and nothing is precomputed.
/// </summary>
public sealed class ComplianceHandler(
    IEverdueDbContext db,
    ITenantProvider tenants,
    IUserDirectory users,
    IClock clock,
    IOptions<InsightsOptions> options)
    : IRequestHandler<ComplianceQuery, PagedResult<ComplianceRowDto>>
{
    public async Task<PagedResult<ComplianceRowDto>> Handle(
        ComplianceQuery request,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var settings = options.Value;
        var timeZone = await tenants.GetTimeZoneAsync(cancellationToken);
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);

        var filter = request.Scope();
        var window = request.Window(BucketKind.Week, timeZone, now, settings);

        var occurrences = await new OccurrenceLedgerReader(db).InWindowAsync(filter, window, cancellationToken);

        var byResponsibility = occurrences
            .GroupBy(o => o.ResponsibilityId)
            .ToDictionary(group => group.Key, group => ComplianceCalculator.Build(group, window, now));

        // A responsibility whose periods have not been judged yet has no rate to show. Listing it at
        // 0% would report silence as failure.
        var measured = byResponsibility.Where(pair => pair.Value.Tally.Concluded > 0).ToArray();

        var labels = await new ResponsibilityLabelReader(db).ForAsync(
            measured.Select(pair => pair.Key).ToArray(),
            cancellationToken);

        var directory = await users.MapAsync(labels.Values.Select(label => label.OwnerUserId), cancellationToken);

        // Retired and paused responsibilities keep their history — the work did happen — and each row
        // says which it is, so a manager does not chase an obligation nobody is expected to meet any
        // more. Chronic detection, which is about what to fix now, looks at active ones only.
        var rows = measured
            .Where(pair => labels.ContainsKey(pair.Key))
            .Select(pair => InsightsRows.Compliance(
                labels[pair.Key],
                pair.Value,
                directory,
                filter,
                window,
                now,
                settings.MinOccurrencesForRate))
            .ToArray();

        // Sorted here rather than in SQL because every sortable column is computed — the same trade
        // the entity-health report already documents, at the same scale (hundreds of rows).
        var sorted = Sort(rows, request.ResolvedSort, request.ResolvedDescending).ToArray();
        var items = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToArray();

        return new PagedResult<ComplianceRowDto>(items, sorted.Length, page, pageSize);
    }

    private static IEnumerable<ComplianceRowDto> Sort(
        IEnumerable<ComplianceRowDto> rows,
        ComplianceSort sort,
        bool descending)
    {
        if (sort == ComplianceSort.Rate)
        {
            // A withheld rate is not a low rate: those rows sort last in both directions rather than
            // pretending to be the best or the worst performers.
            var ordered = rows.OrderBy(r => r.Rate is null);

            return (descending
                    ? ordered.ThenByDescending(r => r.Rate)
                    : ordered.ThenBy(r => r.Rate))
                .ThenBy(r => r.Title);
        }

        Func<ComplianceRowDto, int> key = sort switch
        {
            ComplianceSort.OnTime => row => row.OnTime,
            ComplianceSort.Late => row => row.Late,
            ComplianceSort.Concluded => row => row.Concluded,
            ComplianceSort.Missed => row => row.Missed,
            _ => _ => 0,
        };

        if (sort == ComplianceSort.Title)
        {
            return descending
                ? rows.OrderByDescending(r => r.Title)
                : rows.OrderBy(r => r.Title);
        }

        return (descending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ThenBy(r => r.Title);
    }
}
