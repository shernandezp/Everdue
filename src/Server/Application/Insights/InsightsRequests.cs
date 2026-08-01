using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Insights;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// Which work an insight is about. Enum-valued parameters are strings on the wire and parsed
/// case-insensitively (<see cref="EnumQuery"/>), exactly as the v1 reports do.
/// </summary>
public interface IInsightsScope
{
    Guid? OwnerId { get; }

    Guid? DepartmentId { get; }

    Guid? EntityId { get; }

    string? EntityType { get; }
}

/// <summary>
/// Which work, and over what stretch of time. Split from <see cref="IInsightsScope"/> because chronic
/// detection judges each responsibility's own last N periods and has no reporting window — advertising
/// <c>from</c> and <c>bucket</c> on an endpoint that ignores them would be a lie in the OpenAPI document.
/// </summary>
public interface IInsightsQuery : IInsightsScope
{
    DateTimeOffset? From { get; }

    DateTimeOffset? To { get; }

    string? Bucket { get; }

    int? Buckets { get; }
}

public static class InsightsQueryExtensions
{
    public static InsightsFilter Scope(this IInsightsScope query)
        => new(
            query.OwnerId,
            query.DepartmentId,
            query.EntityId,
            EnumQuery.Parse<EntityType>(query.EntityType, nameof(IInsightsScope.EntityType)));

    public static InsightsWindow Window(
        this IInsightsQuery query,
        BucketKind defaultKind,
        TimeZoneInfo timeZone,
        DateTimeOffset now,
        InsightsOptions options)
        => InsightsWindow.Resolve(
            EnumQuery.ParseOr(query.Bucket, nameof(IInsightsQuery.Bucket), defaultKind),
            query.From,
            query.To,
            query.Buckets,
            timeZone,
            now,
            options);
}

public enum ComplianceSort
{
    Title = 0,
    OnTime = 1,
    Late = 2,
    Missed = 3,
    Concluded = 4,
    Rate = 5,
}

public enum ReliabilitySort
{
    Name = 0,
    OnTime = 1,
    Late = 2,
    Missed = 3,
    Concluded = 4,
    Rate = 5,
    ExternallyBlocked = 6,
    BlockedDays = 7,
}

/// <summary>
/// Compliance per responsibility. Sorted by misses descending by default: the manager surface leads
/// with what needs attention, never with a ranking.
/// </summary>
public sealed record ComplianceQuery(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? EntityId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Bucket = null,
    int? Buckets = null,
    string? Sort = null,
    bool? Descending = null,
    int? Page = null,
    int? PageSize = null) : IQuery<PagedResult<ComplianceRowDto>>, IInsightsQuery
{
    public ComplianceSort ResolvedSort => EnumQuery.ParseOr(Sort, nameof(Sort), ComplianceSort.Missed);

    /// <summary>
    /// Nullable rather than defaulted, because <c>[AsParameters]</c> binds an absent bool to
    /// <c>default</c> and would silently turn "attention first" into "attention last".
    /// </summary>
    public bool ResolvedDescending => Descending ?? true;
}

public sealed record ResponsibilityComplianceQuery(
    Guid ResponsibilityId,
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? EntityId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Bucket = null,
    int? Buckets = null) : IQuery<ResponsibilityComplianceDto>, IInsightsQuery;

public sealed record ReliabilityQuery(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? EntityId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Bucket = null,
    int? Buckets = null,
    string? Sort = null,
    bool? Descending = null) : IQuery<IReadOnlyList<ReliabilityRowDto>>, IInsightsQuery
{
    public ReliabilitySort ResolvedSort => EnumQuery.ParseOr(Sort, nameof(Sort), ReliabilitySort.Missed);

    /// <summary>See <see cref="ComplianceQuery.ResolvedDescending"/> — an absent bool binds to false.</summary>
    public bool ResolvedDescending => Descending ?? true;
}

/// <summary>Completed work per entity per bucket. Months by default — a week of one client is noise.</summary>
public sealed record ConcentrationQuery(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? EntityId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Bucket = null,
    int? Buckets = null) : IQuery<ConcentrationSeriesDto>, IInsightsQuery;

public sealed record HoldAgingQuery(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? EntityId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Bucket = null,
    int? Buckets = null) : IQuery<HoldAgingDto>, IInsightsQuery;

/// <summary>
/// Chronically delayed responsibilities. Deliberately window-independent: the rule is "K misses in the
/// last N periods of that responsibility", and for a yearly obligation the last eight periods are
/// eight years of history — any date bound would silently exempt exactly those.
/// </summary>
public sealed record ChronicDelayQuery(
    int? Limit = null,
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? EntityId = null,
    string? EntityType = null) : IQuery<IReadOnlyList<ChronicResponsibilityDto>>, IInsightsScope;
