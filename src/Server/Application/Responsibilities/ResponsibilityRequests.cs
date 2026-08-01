using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Responsibilities;

public sealed record ListResponsibilitiesQuery(
    string? Search = null,
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? EntityId = null,
    bool IncludeInactive = false,
    int? Page = null,
    int? PageSize = null) : IQuery<PagedResult<ResponsibilityDto>>;

public sealed record GetResponsibilityQuery(Guid Id) : IQuery<ResponsibilityDto>;

public sealed record CreateResponsibilityCommand(
    [property: Required, MaxLength(300)] string Title,
    [property: MaxLength(4000)] string? Description,
    Guid OwnerUserId,
    Guid? DepartmentId,
    Guid? EntityId,
    RecurrenceKind RecurrenceKind,
    int? DaysOfWeekMask,
    [property: Range(1, 31)] int? DayOfMonth,
    [property: Range(1, 12)] int? MonthOfYear,
    DateOnly StartDate,

    /// <summary>
    /// The two server-enforced completion rules. Both default off, and switching one on applies from the
    /// next completion attempt — nothing already completed is reopened.
    /// </summary>
    bool RequireChecklistToComplete = false,
    bool RequireAttachmentToComplete = false) : ICommand<ResponsibilityDto>;

public sealed record UpdateResponsibilityCommand(
    Guid Id,
    [property: Required, MaxLength(300)] string Title,
    [property: MaxLength(4000)] string? Description,
    Guid OwnerUserId,
    Guid? DepartmentId,
    Guid? EntityId,
    RecurrenceKind RecurrenceKind,
    int? DaysOfWeekMask,
    [property: Range(1, 31)] int? DayOfMonth,
    [property: Range(1, 12)] int? MonthOfYear,
    DateOnly StartDate,
    bool Active,
    bool RequireChecklistToComplete = false,
    bool RequireAttachmentToComplete = false) : ICommand<ResponsibilityDto>;

public sealed record DeactivateResponsibilityCommand(Guid Id) : ICommand<ResponsibilityDto>;

/// <summary>
/// Pause through the end of <paramref name="Until"/> (tenant-local, inclusive). Periods that fall
/// wholly inside the window are skipped on resume — a sanctioned pause is never a miss.
/// </summary>
public sealed record PauseResponsibilityCommand(Guid Id, DateOnly Until) : ICommand<ResponsibilityDto>;

public sealed record ResumeResponsibilityCommand(Guid Id) : ICommand<ResponsibilityDto>;
