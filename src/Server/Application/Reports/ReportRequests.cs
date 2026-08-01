using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Reports;

/// <summary>The filters every report accepts, resolved into typed values once.</summary>
public sealed record ReportFilter(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    EntityType? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

/// <summary>
/// Enum-valued query parameters are strings on the wire and parsed case-insensitively, so a
/// hand-written or lower-cased link works instead of failing the model binder (see <see cref="EnumQuery"/>).
/// </summary>
public sealed record ExceptionsReportQuery(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<ExceptionsReportDto>
{
    public ReportFilter Filter => new(
        OwnerId,
        DepartmentId,
        EnumQuery.Parse<EntityType>(EntityType, nameof(EntityType)),
        From,
        To);
}

public enum EntityHealthSort
{
    Name = 0,
    Open = 1,
    Overdue = 2,
    Missed30 = 3,
    Missed60 = 4,
    Missed90 = 5,
    OnHold = 6,
    DaysSinceLastActivity = 7,
}

public sealed record EntityHealthQuery(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Sort = null,
    bool Descending = false,
    string? Search = null,
    int? Page = null,
    int? PageSize = null) : IQuery<PagedResult<EntityHealthRowDto>>
{
    public ReportFilter Filter => new(
        OwnerId,
        DepartmentId,
        EnumQuery.Parse<EntityType>(EntityType, nameof(EntityType)),
        From,
        To);

    public EntityHealthSort ResolvedSort => EnumQuery.ParseOr(Sort, nameof(Sort), EntityHealthSort.Name);
}

public sealed record NeglectReportQuery(
    int Days = 90,
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<IReadOnlyList<NeglectRowDto>>
{
    public ReportFilter Filter => new(
        OwnerId,
        DepartmentId,
        EnumQuery.Parse<EntityType>(EntityType, nameof(EntityType)),
        From,
        To);
}

public sealed record BlockedByEntityQuery(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<IReadOnlyList<BlockedByEntityGroupDto>>
{
    public ReportFilter Filter => new(
        OwnerId,
        DepartmentId,
        EnumQuery.Parse<EntityType>(EntityType, nameof(EntityType)),
        From,
        To);
}

public sealed record EntityTimelineQuery(
    Guid EntityId,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? OwnerId = null,
    Guid? DepartmentId = null) : IQuery<EntityTimelineDto>;
