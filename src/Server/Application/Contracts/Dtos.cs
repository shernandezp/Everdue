using Everdue.Server.Domain;

namespace Everdue.Server.Application.Contracts;

// DTOs are always separate from domain entities — an EF entity is never returned from the API.

public sealed record TenantSettingsDto(
    Guid Id,
    string Name,
    string TimeZoneId,
    int DigestHourLocal,
    string DefaultLanguage,
    int ReminderHourLocal = 8,
    bool CanUseSystemChannels = true,

    /// <summary>
    /// This workspace holds seeded demo data. Read-only here — <c>PUT /settings/tenant</c> ignores it, and the
    /// only way to change it is the demo endpoint, which wipes the tenant on the way through.
    ///
    /// <para>Carried on the tenant DTO rather than left on the admin-only demo endpoint so that <em>every</em>
    /// signed-in user's client can say so. A member who cannot reach the settings screen is exactly the person
    /// who would otherwise put real work into an install full of invented history.</para>
    /// </summary>
    bool DemoMode = false);

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    string? PreferredLanguage,
    bool Active,
    bool MustChangePassword,

    /// <summary>Administrator-maintained. Non-administrators never see a colleague's number.</summary>
    string? WhatsAppPhoneE164 = null);

/// <summary>Everything the SPA needs at boot: who I am, what I may see, and how to render it.</summary>
public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    string Language,
    bool MustChangePassword,
    TenantSettingsDto Tenant);

/// <summary>
/// <paramref name="CustomFields"/> is display-only reference information, resolved against the tenant's
/// definitions. It is deliberately the only extra thing an entity carries: no filter, sort, report
/// column or webhook field reads it.
/// </summary>
public sealed record EntityDto(
    Guid Id,
    string Name,
    EntityType Type,
    bool Active,
    IReadOnlyList<EntityCustomFieldValueDto> CustomFields);

public sealed record DepartmentDto(Guid Id, string Name, bool Active);

public sealed record ResponsibilityDto(
    Guid Id,
    string Title,
    string? Description,
    Guid OwnerUserId,
    string OwnerDisplayName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? EntityId,
    string? EntityName,
    RecurrenceKind RecurrenceKind,
    int? DaysOfWeekMask,
    int? DayOfMonth,
    int? MonthOfYear,
    DateOnly StartDate,
    bool Active,
    DateTimeOffset? PausedUntil,
    DateOnly? NextScheduledDate,

    /// <summary>Server-enforced completion rules. Occurrences only — a one-off task has no responsibility.</summary>
    bool RequireChecklistToComplete = false,
    bool RequireAttachmentToComplete = false,

    /// <summary>How many template lines the responsibility carries, so the list can show it without a second call.</summary>
    int ChecklistItemCount = 0);

public sealed record WorkItemDto(
    Guid Id,
    Guid? ResponsibilityId,
    string? ResponsibilityTitle,
    string Title,
    string? Description,
    Guid OwnerUserId,
    string OwnerDisplayName,
    Guid? EntityId,
    string? EntityName,
    EntityType? EntityType,
    Guid? DepartmentId,
    string? DepartmentName,
    DateTimeOffset DueDate,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    WorkItemStatus Status,
    HoldReason? HoldReason,
    string? HoldReasonText,
    bool IsOverdue,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    Guid? CompletedByUserId,
    string? CompletedByDisplayName,

    /// <summary>
    /// Checklist progress, or null when the item has no checklist — so a badge is absent rather than
    /// reading "0/0". Filled from one grouped query for the whole page, never one query per row.
    /// </summary>
    int? ChecklistTotal = null,
    int? ChecklistChecked = null);

public sealed record WorkItemEventDto(
    Guid Id,
    Guid? UserId,
    string? UserDisplayName,
    DateTimeOffset Timestamp,
    WorkItemEventType EventType,
    WorkItemStatus? FromStatus,
    WorkItemStatus? ToStatus,
    string? DataJson);

public sealed record ResponsibilityEventDto(
    Guid Id,
    Guid UserId,
    string UserDisplayName,
    DateTimeOffset Timestamp,
    ResponsibilityEventType EventType,
    string? DataJson);

public sealed record CommentDto(
    Guid Id,
    Guid WorkItemId,
    Guid UserId,
    string UserDisplayName,
    string Body,
    DateTimeOffset CreatedAt);

/// <summary>What the item drawer renders: the item, its history, its comments and what may be done to it next.</summary>
public sealed record WorkItemDetailDto(
    WorkItemDto Item,
    IReadOnlyList<WorkItemEventDto> Events,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<WorkItemStatus> AllowedTransitions,

    /// <summary>
    /// The item's checklist, in order. Empty for the many items that have none.
    /// </summary>
    IReadOnlyList<ChecklistItemDto> Checklist,

    /// <summary>
    /// What stands in the way of completing it, or null when nothing does. <c>Completed</c> stays in
    /// <paramref name="AllowedTransitions"/> either way — it <em>is</em> a legal transition, and the
    /// server refuses it with a reason rather than pretending the move does not exist.
    /// </summary>
    CompletionRequirementsDto? CompletionRequirements);
