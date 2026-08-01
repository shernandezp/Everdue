using System.Globalization;
using System.Text.Json;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Common;

/// <summary>
/// The field names written into a responsibility event's diff payload. A stored contract, exactly
/// like <see cref="WorkItemFields"/>: renaming a value here stops existing history from rendering,
/// and the compiler cannot tell you.
/// </summary>
public static class ResponsibilityFields
{
    public const string Title = "title";
    public const string Description = "description";
    public const string Owner = "ownerUserId";
    public const string Entity = "entityId";
    public const string Department = "departmentId";
    public const string RecurrenceKind = "recurrenceKind";
    public const string DaysOfWeekMask = "daysOfWeekMask";
    public const string DayOfMonth = "dayOfMonth";
    public const string MonthOfYear = "monthOfYear";
    public const string StartDate = "startDate";
    public const string Active = "active";
    public const string RequireChecklistToComplete = "requireChecklistToComplete";
    public const string RequireAttachmentToComplete = "requireAttachmentToComplete";
}

/// <summary>
/// Every responsibility mutation writes one of these. The rules that generate occurrences decide
/// what the ledger will ever contain, so their history is part of the ledger's trustworthiness:
/// "who changed this weekly rule to yearly, and when" must be answerable.
/// </summary>
public static class ResponsibilityEventFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ResponsibilityEvent Created(Responsibility responsibility, Guid userId, DateTimeOffset at)
        => Build(responsibility, userId, at, ResponsibilityEventType.Created, null);

    /// <summary>An edit that moved the owner is typed as a reassignment, however it was made.</summary>
    public static ResponsibilityEvent Updated(
        Responsibility responsibility,
        Guid userId,
        DateTimeOffset at,
        IReadOnlyList<FieldChange> changes)
        => Build(
            responsibility,
            userId,
            at,
            changes.Any(c => c.Field == ResponsibilityFields.Owner)
                ? ResponsibilityEventType.Reassigned
                : ResponsibilityEventType.Updated,
            new { changes });

    public static ResponsibilityEvent Reassigned(
        Responsibility responsibility,
        Guid userId,
        DateTimeOffset at,
        IReadOnlyList<FieldChange> changes)
        => Build(responsibility, userId, at, ResponsibilityEventType.Reassigned, new { changes });

    public static ResponsibilityEvent Paused(Responsibility responsibility, Guid userId, DateTimeOffset at, DateOnly until)
        => Build(responsibility, userId, at, ResponsibilityEventType.Paused, new { until });

    public static ResponsibilityEvent Resumed(Responsibility responsibility, Guid userId, DateTimeOffset at)
        => Build(responsibility, userId, at, ResponsibilityEventType.Resumed, null);

    public static ResponsibilityEvent Deactivated(Responsibility responsibility, Guid userId, DateTimeOffset at)
        => Build(responsibility, userId, at, ResponsibilityEventType.Deactivated, null);

    /// <summary>Diff-payload string forms, invariant so history reads the same on any machine.</summary>
    public static string? Value(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string? Value(int? number) => number?.ToString(CultureInfo.InvariantCulture);

    public static string Value(bool flag) => flag ? "true" : "false";

    private static ResponsibilityEvent Build(
        Responsibility responsibility,
        Guid userId,
        DateTimeOffset at,
        ResponsibilityEventType type,
        object? data)
        => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = responsibility.TenantId,
            ResponsibilityId = responsibility.Id,
            UserId = userId,
            Timestamp = at,
            EventType = type,
            DataJson = data is null ? null : JsonSerializer.Serialize(data, JsonOptions),
        };
}
