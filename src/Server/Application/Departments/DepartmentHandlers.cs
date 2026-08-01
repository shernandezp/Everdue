using System.Linq.Expressions;
using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;

namespace Everdue.Server.Application.Departments;

internal static class DepartmentMapping
{
    public static readonly Expression<Func<Department, DepartmentDto>> Projection =
        d => new DepartmentDto(d.Id, d.Name, d.Active);
}

public sealed class ListDepartmentsHandler(IEverdueDbContext db)
    : IRequestHandler<ListDepartmentsQuery, PagedResult<DepartmentDto>>
{
    public async Task<PagedResult<DepartmentDto>> Handle(ListDepartmentsQuery request, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = Paging.Normalize(request.Page, request.PageSize);

        var query = db.Departments.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(d => d.Active);
        }

        if (SearchPattern.For(request.Search) is { } pattern)
        {
            query = query.Where(d => EF.Functions.Like(d.Name.ToLower(), pattern, SearchPattern.Escape));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(d => d.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(DepartmentMapping.Projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<DepartmentDto>(items, total, page, pageSize);
    }
}

public sealed class GetDepartmentHandler(IEverdueDbContext db) : IRequestHandler<GetDepartmentQuery, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(GetDepartmentQuery request, CancellationToken cancellationToken = default)
        => await db.Departments.AsNoTracking().Where(d => d.Id == request.Id).Select(DepartmentMapping.Projection)
               .FirstOrDefaultAsync(cancellationToken)
           ?? throw new NotFoundException(ResourceNames.Department, request.Id);
}

public sealed class CreateDepartmentHandler(IEverdueDbContext db) : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (await db.Departments.AnyAsync(d => d.Name == name, cancellationToken))
        {
            throw new ValidationException($"A department named '{name}' already exists.");
        }

        var department = new Department { Id = Guid.CreateVersion7(), Name = name, Active = true };
        db.Departments.Add(department);
        await db.SaveChangesAsync(cancellationToken);

        return new DepartmentDto(department.Id, department.Name, department.Active);
    }
}

public sealed class UpdateDepartmentHandler(IEverdueDbContext db) : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken = default)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException(ResourceNames.Department, request.Id);

        var name = request.Name.Trim();

        if (await db.Departments.AnyAsync(d => d.Id != request.Id && d.Name == name, cancellationToken))
        {
            throw new ValidationException($"A department named '{name}' already exists.");
        }

        department.Name = name;
        department.Active = request.Active;
        await db.SaveChangesAsync(cancellationToken);

        return new DepartmentDto(department.Id, department.Name, department.Active);
    }
}

public sealed class DeactivateDepartmentHandler(IEverdueDbContext db) : IRequestHandler<DeactivateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(DeactivateDepartmentCommand request, CancellationToken cancellationToken = default)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException(ResourceNames.Department, request.Id);

        department.Active = false;
        await db.SaveChangesAsync(cancellationToken);

        return new DepartmentDto(department.Id, department.Name, department.Active);
    }
}
