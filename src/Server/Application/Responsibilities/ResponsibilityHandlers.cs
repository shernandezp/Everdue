using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Everdue.Server.Domain.Recurrence;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Responsibilities;

/// <summary>Shared row shape so list and single-item reads project identically.</summary>
internal sealed record ResponsibilityRow(
    Guid Id,
    string Title,
    string? Description,
    Guid OwnerUserId,
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
    bool RequireChecklistToComplete,
    bool RequireAttachmentToComplete);

internal static class ResponsibilityMapping
{
    public static IQueryable<ResponsibilityRow> Project(IQueryable<Responsibility> query)
        => query.Select(r => new ResponsibilityRow(
            r.Id,
            r.Title,
            r.Description,
            r.OwnerUserId,
            r.DepartmentId,
            r.DepartmentId == null ? null : r.Department!.Name,
            r.EntityId,
            r.EntityId == null ? null : r.Entity!.Name,
            r.RecurrenceKind,
            r.DaysOfWeekMask,
            r.DayOfMonth,
            r.MonthOfYear,
            r.StartDate,
            r.Active,
            r.PausedUntil,
            r.RequireChecklistToComplete,
            r.RequireAttachmentToComplete));

    public static ResponsibilityDto ToDto(ResponsibilityRow row, string ownerDisplayName, DateOnly today, int checklistItemCount)
    {
        var rule = new RecurrenceRule(row.RecurrenceKind, row.DaysOfWeekMask, row.DayOfMonth, row.MonthOfYear, row.StartDate);

        // A preview of the next date the engine would use, not a promise: a pause or deactivation
        // can still intervene before the tick that would create it.
        DateOnly? next = row.Active && rule.Validate() is null
            ? RecurrenceCalculator.FirstScheduledOnOrAfter(rule, row.StartDate > today ? row.StartDate : today)
            : null;

        return new ResponsibilityDto(
            row.Id,
            row.Title,
            row.Description,
            row.OwnerUserId,
            ownerDisplayName,
            row.DepartmentId,
            row.DepartmentName,
            row.EntityId,
            row.EntityName,
            row.RecurrenceKind,
            row.DaysOfWeekMask,
            row.DayOfMonth,
            row.MonthOfYear,
            row.StartDate,
            row.Active,
            row.PausedUntil,
            next,
            row.RequireChecklistToComplete,
            row.RequireAttachmentToComplete,
            checklistItemCount);
    }
}

internal sealed class ResponsibilityDtoBuilder(
    IEverdueDbContext db,
    IUserDirectory users,
    ITenantProvider tenants,
    IClock clock)
{
    public async Task<IReadOnlyList<ResponsibilityDto>> BuildAsync(IReadOnlyList<ResponsibilityRow> rows, CancellationToken cancellationToken)
    {
        var owners = await users.MapAsync(rows.Select(r => r.OwnerUserId), cancellationToken);
        var timeZone = await tenants.GetTimeZoneAsync(cancellationToken);
        var today = TenantTime.LocalDate(clock.UtcNow, timeZone);

        // One grouped count for the whole page. The list shows "5 items" beside the template switch, and
        // a per-row count would make the responsibilities screen one query plus N.
        var ids = rows.Select(r => r.Id).ToArray();

        var templateCounts = new Dictionary<Guid, int>();

        if (ids.Length > 0)
        {
            templateCounts = await db.ChecklistTemplateItems.AsNoTracking()
                .Where(t => ids.Contains(t.ResponsibilityId))
                .GroupBy(t => t.ResponsibilityId)
                .Select(g => new { ResponsibilityId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.ResponsibilityId, g => g.Count, cancellationToken);
        }

        return rows
            .Select(r => ResponsibilityMapping.ToDto(
                r,
                owners.TryGetValue(r.OwnerUserId, out var owner) ? owner.DisplayName : "—",
                today,
                templateCounts.TryGetValue(r.Id, out var count) ? count : 0))
            .ToArray();
    }

    public async Task<ResponsibilityDto> BuildAsync(ResponsibilityRow row, CancellationToken cancellationToken)
        => (await BuildAsync([row], cancellationToken))[0];
}

public sealed class ListResponsibilitiesHandler(IEverdueDbContext db, IUserDirectory users, ITenantProvider tenants, IClock clock)
    : IRequestHandler<ListResponsibilitiesQuery, PagedResult<ResponsibilityDto>>
{
    public async Task<PagedResult<ResponsibilityDto>> Handle(ListResponsibilitiesQuery request, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);

        var query = db.Responsibilities.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(r => r.Active);
        }

        if (request.OwnerId is { } ownerId)
        {
            query = query.Where(r => r.OwnerUserId == ownerId);
        }

        if (request.DepartmentId is { } departmentId)
        {
            query = query.Where(r => r.DepartmentId == departmentId);
        }

        if (request.EntityId is { } entityId)
        {
            query = query.Where(r => r.EntityId == entityId);
        }

        if (SearchPattern.For(request.Search) is { } pattern)
        {
            query = query.Where(r => EF.Functions.Like(r.Title.ToLower(), pattern, SearchPattern.Escape));
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await ResponsibilityMapping
            .Project(query.OrderBy(r => r.Title).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);

        var items = await new ResponsibilityDtoBuilder(db, users, tenants, clock).BuildAsync(rows, cancellationToken);
        return new PagedResult<ResponsibilityDto>(items, total, page, pageSize);
    }
}

public sealed class GetResponsibilityHandler(IEverdueDbContext db, IUserDirectory users, ITenantProvider tenants, IClock clock)
    : IRequestHandler<GetResponsibilityQuery, ResponsibilityDto>
{
    public async Task<ResponsibilityDto> Handle(GetResponsibilityQuery request, CancellationToken cancellationToken = default)
    {
        var row = await ResponsibilityMapping
                      .Project(db.Responsibilities.AsNoTracking().Where(r => r.Id == request.Id))
                      .FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException(ResourceNames.Responsibility, request.Id);

        return await new ResponsibilityDtoBuilder(db, users, tenants, clock).BuildAsync(row, cancellationToken);
    }
}

public sealed class CreateResponsibilityHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    ITenantProvider tenants,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<CreateResponsibilityCommand, ResponsibilityDto>
{
    public async Task<ResponsibilityDto> Handle(CreateResponsibilityCommand request, CancellationToken cancellationToken = default)
    {
        await ResponsibilityValidation.EnsureValidAsync(
            db,
            users,
            request.OwnerUserId,
            request.DepartmentId,
            request.EntityId,
            new RecurrenceRule(request.RecurrenceKind, request.DaysOfWeekMask, request.DayOfMonth, request.MonthOfYear, request.StartDate),
            cancellationToken);

        var responsibility = new Responsibility
        {
            Id = Guid.CreateVersion7(),
            Title = request.Title.Trim(),
            Description = request.Description,
            OwnerUserId = request.OwnerUserId,
            DepartmentId = request.DepartmentId,
            EntityId = request.EntityId,
            RecurrenceKind = request.RecurrenceKind,
            DaysOfWeekMask = request.RecurrenceKind == RecurrenceKind.WeeklyOnDays ? request.DaysOfWeekMask : null,
            DayOfMonth = request.RecurrenceKind is RecurrenceKind.MonthlyOnDay or RecurrenceKind.Yearly ? request.DayOfMonth : null,
            MonthOfYear = request.RecurrenceKind == RecurrenceKind.Yearly ? request.MonthOfYear : null,
            StartDate = request.StartDate,
            Active = true,
            RequireChecklistToComplete = request.RequireChecklistToComplete,
            RequireAttachmentToComplete = request.RequireAttachmentToComplete,
        };

        db.Responsibilities.Add(responsibility);
        db.ResponsibilityEvents.Add(ResponsibilityEventFactory.Created(responsibility, currentUser.RequireUserId(), clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        return await Reload(db, users, tenants, clock, responsibility.Id, cancellationToken);
    }

    internal static async Task<ResponsibilityDto> Reload(
        IEverdueDbContext db,
        IUserDirectory users,
        ITenantProvider tenants,
        IClock clock,
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await ResponsibilityMapping
                      .Project(db.Responsibilities.AsNoTracking().Where(r => r.Id == id))
                      .FirstAsync(cancellationToken);

        return await new ResponsibilityDtoBuilder(db, users, tenants, clock).BuildAsync(row, cancellationToken);
    }
}

public sealed class UpdateResponsibilityHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    ITenantProvider tenants,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<UpdateResponsibilityCommand, ResponsibilityDto>
{
    public async Task<ResponsibilityDto> Handle(UpdateResponsibilityCommand request, CancellationToken cancellationToken = default)
    {
        var responsibility = await db.Responsibilities.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                             ?? throw new NotFoundException(ResourceNames.Responsibility, request.Id);

        await ResponsibilityValidation.EnsureValidAsync(
            db,
            users,
            request.OwnerUserId,
            request.DepartmentId,
            request.EntityId,
            new RecurrenceRule(request.RecurrenceKind, request.DaysOfWeekMask, request.DayOfMonth, request.MonthOfYear, request.StartDate),
            cancellationToken);

        // A responsibility edit rewrites what the ledger will record from here on — a rule change,
        // a moved start date, a deactivation. The diff is computed before anything is overwritten,
        // for the same reason work-item edits do it: the old value is the evidence.
        var normalizedDaysOfWeekMask = request.RecurrenceKind == RecurrenceKind.WeeklyOnDays ? request.DaysOfWeekMask : null;
        var normalizedDayOfMonth = request.RecurrenceKind is RecurrenceKind.MonthlyOnDay or RecurrenceKind.Yearly ? request.DayOfMonth : null;
        var normalizedMonthOfYear = request.RecurrenceKind == RecurrenceKind.Yearly ? request.MonthOfYear : null;

        var title = request.Title.Trim();

        var changes = new FieldChangeSet()
            .Track(ResponsibilityFields.Title, responsibility.Title, title)
            .Track(ResponsibilityFields.Description, responsibility.Description, request.Description)
            .Track(ResponsibilityFields.Owner, responsibility.OwnerUserId, request.OwnerUserId)
            .Track(ResponsibilityFields.Entity, responsibility.EntityId, request.EntityId)
            .Track(ResponsibilityFields.Department, responsibility.DepartmentId, request.DepartmentId)
            .Track(ResponsibilityFields.RecurrenceKind, responsibility.RecurrenceKind.ToString(), request.RecurrenceKind.ToString())
            .Track(ResponsibilityFields.DaysOfWeekMask, ResponsibilityEventFactory.Value(responsibility.DaysOfWeekMask), ResponsibilityEventFactory.Value(normalizedDaysOfWeekMask))
            .Track(ResponsibilityFields.DayOfMonth, ResponsibilityEventFactory.Value(responsibility.DayOfMonth), ResponsibilityEventFactory.Value(normalizedDayOfMonth))
            .Track(ResponsibilityFields.MonthOfYear, ResponsibilityEventFactory.Value(responsibility.MonthOfYear), ResponsibilityEventFactory.Value(normalizedMonthOfYear))
            .Track(ResponsibilityFields.StartDate, ResponsibilityEventFactory.Value(responsibility.StartDate), ResponsibilityEventFactory.Value(request.StartDate))
            .Track(ResponsibilityFields.Active, ResponsibilityEventFactory.Value(responsibility.Active), ResponsibilityEventFactory.Value(request.Active))
            .Track(ResponsibilityFields.RequireChecklistToComplete, ResponsibilityEventFactory.Value(responsibility.RequireChecklistToComplete), ResponsibilityEventFactory.Value(request.RequireChecklistToComplete))
            .Track(ResponsibilityFields.RequireAttachmentToComplete, ResponsibilityEventFactory.Value(responsibility.RequireAttachmentToComplete), ResponsibilityEventFactory.Value(request.RequireAttachmentToComplete));

        responsibility.Title = title;
        responsibility.Description = request.Description;
        responsibility.OwnerUserId = request.OwnerUserId;
        responsibility.DepartmentId = request.DepartmentId;
        responsibility.EntityId = request.EntityId;
        responsibility.RecurrenceKind = request.RecurrenceKind;
        responsibility.DaysOfWeekMask = request.RecurrenceKind == RecurrenceKind.WeeklyOnDays ? request.DaysOfWeekMask : null;
        responsibility.DayOfMonth = request.RecurrenceKind is RecurrenceKind.MonthlyOnDay or RecurrenceKind.Yearly ? request.DayOfMonth : null;
        responsibility.MonthOfYear = request.RecurrenceKind == RecurrenceKind.Yearly ? request.MonthOfYear : null;
        responsibility.StartDate = request.StartDate;
        responsibility.Active = request.Active;
        responsibility.RequireChecklistToComplete = request.RequireChecklistToComplete;
        responsibility.RequireAttachmentToComplete = request.RequireAttachmentToComplete;

        if (changes.Any)
        {
            db.ResponsibilityEvents.Add(ResponsibilityEventFactory.Updated(
                responsibility, currentUser.RequireUserId(), clock.UtcNow, changes.Changes));
        }

        await db.SaveChangesAsync(cancellationToken);

        return await CreateResponsibilityHandler.Reload(db, users, tenants, clock, responsibility.Id, cancellationToken);
    }
}

public sealed class DeactivateResponsibilityHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    ITenantProvider tenants,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<DeactivateResponsibilityCommand, ResponsibilityDto>
{
    public async Task<ResponsibilityDto> Handle(DeactivateResponsibilityCommand request, CancellationToken cancellationToken = default)
    {
        var responsibility = await db.Responsibilities.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                             ?? throw new NotFoundException(ResourceNames.Responsibility, request.Id);

        if (responsibility.Active)
        {
            db.ResponsibilityEvents.Add(ResponsibilityEventFactory.Deactivated(
                responsibility, currentUser.RequireUserId(), clock.UtcNow));
        }

        responsibility.Active = false;
        await db.SaveChangesAsync(cancellationToken);

        return await CreateResponsibilityHandler.Reload(db, users, tenants, clock, responsibility.Id, cancellationToken);
    }
}

public sealed class PauseResponsibilityHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    ITenantProvider tenants,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<PauseResponsibilityCommand, ResponsibilityDto>
{
    public async Task<ResponsibilityDto> Handle(PauseResponsibilityCommand request, CancellationToken cancellationToken = default)
    {
        var responsibility = await db.Responsibilities.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                             ?? throw new NotFoundException(ResourceNames.Responsibility, request.Id);

        var timeZone = await tenants.GetTimeZoneAsync(cancellationToken);
        var today = TenantTime.LocalDate(clock.UtcNow, timeZone);

        if (request.Until < today)
        {
            throw new ValidationException("A pause must end today or later.");
        }

        // Inclusive of the chosen date: work resumes at 00:00 the following local day.
        responsibility.PausedUntil = TenantTime.StartOfDay(request.Until.AddDays(1), timeZone);
        db.ResponsibilityEvents.Add(ResponsibilityEventFactory.Paused(
            responsibility, currentUser.RequireUserId(), clock.UtcNow, request.Until));
        await db.SaveChangesAsync(cancellationToken);

        return await CreateResponsibilityHandler.Reload(db, users, tenants, clock, responsibility.Id, cancellationToken);
    }
}

public sealed class ResumeResponsibilityHandler(
    IEverdueDbContext db,
    IUserDirectory users,
    ITenantProvider tenants,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<ResumeResponsibilityCommand, ResponsibilityDto>
{
    public async Task<ResponsibilityDto> Handle(ResumeResponsibilityCommand request, CancellationToken cancellationToken = default)
    {
        var responsibility = await db.Responsibilities.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                             ?? throw new NotFoundException(ResourceNames.Responsibility, request.Id);

        // Ending the pause *now* rather than erasing it: the engine still needs the window's end to
        // know which periods were sanctioned skips. Clearing the column would turn a pause into a
        // burst of misses on the next tick.
        responsibility.PausedUntil = clock.UtcNow;
        db.ResponsibilityEvents.Add(ResponsibilityEventFactory.Resumed(
            responsibility, currentUser.RequireUserId(), clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        return await CreateResponsibilityHandler.Reload(db, users, tenants, clock, responsibility.Id, cancellationToken);
    }
}

public sealed class GetResponsibilityEventsHandler(IEverdueDbContext db, IUserDirectory users)
    : IRequestHandler<GetResponsibilityEventsQuery, IReadOnlyList<ResponsibilityEventDto>>
{
    public async Task<IReadOnlyList<ResponsibilityEventDto>> Handle(GetResponsibilityEventsQuery request, CancellationToken cancellationToken = default)
    {
        if (!await db.Responsibilities.AnyAsync(r => r.Id == request.Id, cancellationToken))
        {
            throw new NotFoundException(ResourceNames.Responsibility, request.Id);
        }

        var events = await db.ResponsibilityEvents.AsNoTracking()
            .Where(e => e.ResponsibilityId == request.Id)
            .OrderBy(e => e.Timestamp)
            .ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.UserId, e.Timestamp, e.EventType, e.DataJson })
            .ToListAsync(cancellationToken);

        var directory = await users.MapAsync(events.Select(e => e.UserId), cancellationToken);

        return events
            .Select(e => new ResponsibilityEventDto(
                e.Id,
                e.UserId,
                directory.TryGetValue(e.UserId, out var user) ? user.DisplayName : "—",
                e.Timestamp,
                e.EventType,
                e.DataJson))
            .ToArray();
    }
}

internal static class ResponsibilityValidation
{
    public static async Task EnsureValidAsync(
        IEverdueDbContext db,
        IUserDirectory users,
        Guid ownerUserId,
        Guid? departmentId,
        Guid? entityId,
        RecurrenceRule rule,
        CancellationToken cancellationToken)
    {
        if (rule.Validate() is { } problem)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["recurrence"] = [problem] });
        }

        await users.RequireAssignableAsync(ownerUserId, cancellationToken);

        if (departmentId is { } department && !await db.Departments.AnyAsync(d => d.Id == department, cancellationToken))
        {
            throw new NotFoundException(ResourceNames.Department, department);
        }

        if (entityId is { } entity && !await db.Entities.AnyAsync(e => e.Id == entity, cancellationToken))
        {
            throw new NotFoundException(ResourceNames.Entity, entity);
        }
    }
}
