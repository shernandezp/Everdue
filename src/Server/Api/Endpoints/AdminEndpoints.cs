using Common.Mediator;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Tenants;
using Everdue.Server.Application.Users;
using Everdue.Server.Domain;

namespace Everdue.Server.Api.Endpoints;

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record ReassignAllRequest(Guid ToUserId, bool IncludeResponsibilities = true, bool IncludeWorkableItems = true);

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/users").WithTags("Users").RequireAuthorization();

        // Reading the directory is NOT an admin action: assigning a task needs the list of people
        // who can own it, and a member who cannot see their colleagues cannot create work at all.
        // The handler narrows what a non-admin sees. Every write below is admin-only.
        group.MapGet("/", async ([AsParameters] ListUsersQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("Lists users. Members see active users only; administrators see everything.")
            .Produces<IReadOnlyList<UserDto>>();

        group.MapPost("/", async (CreateUserCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(command, cancellationToken);
                return Results.Created($"/api/v1/users/{created.Id}", created);
            })
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("Creates a user. There is no self-service registration in v1.")
            .Produces<UserDto>(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command with { Id = id }, cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("Updates display name, role, language and active flag. Deactivated users keep their history.")
            .Produces<UserDto>();

        group.MapPost("/{id:guid}/password", async (Guid id, ResetPasswordRequest body, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new ResetUserPasswordCommand(id, body.NewPassword), cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("Administrator password reset. Always forces a change at the user's next login.");

        group.MapPost("/{id:guid}/reassign-all", async (Guid id, ReassignAllRequest body, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(
                    new ReassignUserWorkCommand(id, body.ToUserId, body.IncludeResponsibilities, body.IncludeWorkableItems),
                    cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("The departure path: everything this person owns becomes somebody else's, in one call.")
            .Produces<ReassignResultDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return api;
    }

    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/settings").WithTags("Settings");

        group.MapGet("/tenant", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new GetTenantSettingsQuery(), cancellationToken)))
            .RequireAuthorization()
            .Produces<TenantSettingsDto>();

        group.MapPut("/tenant", async (UpdateTenantSettingsCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command, cancellationToken)))
            .RequireAuthorization(ApiPolicies.Admin)
            .WithSummary("Name, IANA time zone, digest hour and default language.")
            .Produces<TenantSettingsDto>();

        return api;
    }
}
