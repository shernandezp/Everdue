using Common.Mediator;
using Everdue.Server.Application.Auth;
using Everdue.Server.Application.Contracts;

namespace Everdue.Server.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command, cancellationToken)))
            .AllowAnonymous()
            // Identity's lockout covers one account under attack; this covers one password tried
            // against every account, which lockout cannot see.
            .RequireRateLimiting(ApiPolicies.AuthRateLimit)
            .WithSummary("Signs in and issues the auth cookie.")
            .Produces<CurrentUserDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/logout", async (ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new LogoutCommand(), cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithSummary("Clears the auth cookie.");

        group.MapGet("/me", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new MeQuery(), cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Everything the SPA needs at boot: identity, role, language and tenant settings.")
            .Produces<CurrentUserDto>();

        group.MapPut("/profile", async (UpdateOwnProfileCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command, cancellationToken)))
            .RequireAuthorization()
            .WithSummary("Updates the signed-in user's display name and language preference.")
            .Produces<CurrentUserDto>();

        group.MapPost("/password", async (ChangeOwnPasswordCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(command, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithSummary("Changes the signed-in user's own password and clears the forced-change flag.");

        return api;
    }
}
