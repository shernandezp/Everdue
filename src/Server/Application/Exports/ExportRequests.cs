using Common.Mediator;
using Everdue.Server.Application.WorkItems;

namespace Everdue.Server.Application.Exports;

/// <summary>Which tabular report is being exported. The exceptions dashboard is deliberately absent.</summary>
public enum ReportExportView
{
    EntityHealth = 0,
    Neglect = 1,
    BlockedByEntity = 2,
}

public enum InsightExportView
{
    Compliance = 0,
    Reliability = 1,
    Concentration = 2,
    HoldAging = 3,
}

/// <summary>
/// The tables a raw dump may read. A fixed allow-list rather than a table name from the caller, which is
/// the difference between an export endpoint and an arbitrary read of the schema.
/// </summary>
public enum RawExportTable
{
    Entities = 0,
    Responsibilities = 1,
    WorkItems = 2,
    WorkItemEvents = 3,
    Comments = 4,
    ChecklistItems = 5,
}

/// <summary>
/// Every export dispatches the query its screen dispatches, unchanged, so the file and the table above it
/// cannot disagree — the same invariant a drill-through gives a number and its rows.
/// </summary>
public sealed record ExportWorkItemsQuery(ListWorkItemsQuery Filter) : IQuery<CsvDocument>;

/// <summary>
/// The report filter as it arrives on the wire, so this endpoint accepts exactly the parameters the
/// report endpoint accepts and hands them straight on.
/// </summary>
public sealed record ExportReportQuery(
    ReportExportView View,
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Days = 90) : IQuery<CsvDocument>;

public sealed record ExportInsightQuery(
    InsightExportView View,
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? EntityId = null,
    string? EntityType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Bucket = null,
    int? Buckets = null) : IQuery<CsvDocument>;

public sealed record ExportRawTableQuery(RawExportTable Table) : IQuery<CsvDocument>;
