using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;

namespace Everdue.Server.Application.Departments;

public sealed record ListDepartmentsQuery(
    string? Search = null,
    bool IncludeInactive = false,
    int? Page = null,
    int? PageSize = null) : IQuery<PagedResult<DepartmentDto>>;

public sealed record GetDepartmentQuery(Guid Id) : IQuery<DepartmentDto>;

public sealed record CreateDepartmentCommand(
    [property: Required, MaxLength(200)] string Name) : ICommand<DepartmentDto>;

public sealed record UpdateDepartmentCommand(
    Guid Id,
    [property: Required, MaxLength(200)] string Name,
    bool Active) : ICommand<DepartmentDto>;

public sealed record DeactivateDepartmentCommand(Guid Id) : ICommand<DepartmentDto>;
