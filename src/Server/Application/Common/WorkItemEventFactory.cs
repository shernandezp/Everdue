using System.Text.Json;
using System.Text.Json.Nodes;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Common;

/// <summary>
/// One field's before and after, as it is written into a <see cref="WorkItemEventType.Updated"/>
/// event. Values are strings so the payload stays readable and stable regardless of the field's type.
/// </summary>
public sealed record FieldChange(string Field, string? From, string? To);

/// <summary>Collects the differences between a work item and the edit being applied to it.</summary>
public sealed class FieldChangeSet
{
    private readonly List<FieldChange> _changes = [];

    public IReadOnlyList<FieldChange> Changes => _changes;

    public bool Any => _changes.Count > 0;

    public FieldChangeSet Track(string field, string? before, string? after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            _changes.Add(new FieldChange(field, before, after));
        }

        return this;
    }

    public FieldChangeSet Track(string field, Guid? before, Guid? after)
        => Track(field, before?.ToString(), after?.ToString());
}

/// <summary>
/// Every mutation writes one of these. Only the item drawer reads them back today, but hold-aging analysis
/// and any future audit log will be built entirely from this table, so the payloads are written properly now.
/// </summary>
public static class WorkItemEventFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WorkItemEvent Created(WorkItem item, Guid? userId, DateTimeOffset at, object? data = null)
        => Build(item, userId, at, WorkItemEventType.Created, null, item.Status, data);

    public static WorkItemEvent StatusChanged(
        WorkItem item,
        Guid? userId,
        DateTimeOffset at,
        WorkItemStatus from,
        WorkItemStatus to,
        object? data = null)
        => Build(item, userId, at, WorkItemEventType.StatusChanged, from, to, data);

    public static WorkItemEvent Rescheduled(
        WorkItem item,
        Guid userId,
        DateTimeOffset at,
        DateTimeOffset previousDueDate,
        DateTimeOffset newDueDate,
        string? note)
        => Build(item, userId, at, WorkItemEventType.Rescheduled, item.Status, item.Status, new
        {
            from = previousDueDate,
            to = newDueDate,
            note,
        });

    public static WorkItemEvent CommentAdded(WorkItem item, Guid userId, DateTimeOffset at, Guid commentId)
        => Build(item, userId, at, WorkItemEventType.CommentAdded, null, null, new { commentId });

    /// <summary>
    /// Records a descriptive edit field by field. The old value matters as much as the new one:
    /// "who moved this off my plate, and when" is the question this table has to be able to answer.
    /// </summary>
    public static WorkItemEvent Updated(
        WorkItem item,
        Guid userId,
        DateTimeOffset at,
        IReadOnlyList<FieldChange> changes)
        => Build(item, userId, at, EventTypeFor(changes), null, null, new { changes });

    /// <summary>
    /// A hand-over, written with the identical payload an edit uses. Same diff, different type —
    /// which is what turns "items reassigned in this period" into an indexed query instead of a scan
    /// through JSON that neither database provider can do portably.
    /// </summary>
    public static WorkItemEvent Reassigned(
        WorkItem item,
        Guid userId,
        DateTimeOffset at,
        IReadOnlyList<FieldChange> changes)
        => Build(item, userId, at, WorkItemEventType.Reassigned, null, null, new { changes });

    /// <summary>
    /// An edit that moved the owner *is* a reassignment, however it was made. Typing it here rather
    /// than at each call site means the board's drag, the drawer's edit and the bulk action all
    /// produce the same history.
    /// </summary>
    /// <summary>
    /// Folds <c>apiKeyId</c> into an event's payload when the write came in over the public API.
    ///
    /// A JSON merge rather than a column: nothing ever queries this — it is read by a human looking at one
    /// item's history — and a nullable column on the ledger's busiest table would cost more than it answers.
    /// </summary>
    public static string WithApiKey(string? dataJson, Guid apiKeyId)
    {
        var node = string.IsNullOrWhiteSpace(dataJson)
            ? new JsonObject()
            : JsonNode.Parse(dataJson) as JsonObject ?? new JsonObject();

        node["apiKeyId"] = apiKeyId.ToString();
        return node.ToJsonString(JsonOptions);
    }

    private static WorkItemEventType EventTypeFor(IReadOnlyList<FieldChange> changes)
        => changes.Any(c => c.Field == WorkItemFields.Owner) ? WorkItemEventType.Reassigned : WorkItemEventType.Updated;

    private static WorkItemEvent Build(
        WorkItem item,
        Guid? userId,
        DateTimeOffset at,
        WorkItemEventType type,
        WorkItemStatus? from,
        WorkItemStatus? to,
        object? data)
        => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = item.TenantId,
            WorkItemId = item.Id,
            UserId = userId,
            Timestamp = at,
            EventType = type,
            FromStatus = from,
            ToStatus = to,
            DataJson = data is null ? null : JsonSerializer.Serialize(data, JsonOptions),
        };
}
