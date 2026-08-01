using Common.Mediator;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Demo;

namespace Everdue.Server.Api.Endpoints;

public static class DemoEndpoints
{
    /// <summary>
    /// Demo mode: fill an empty install with believable history so the dashboards and the reports have
    /// something to show, or clear it back out for real use.
    ///
    /// <para>Administrator-only, and deliberately <em>not</em> marked <c>.AllowApiKey()</c> — <c>ApiKeyGate</c>
    /// therefore refuses it to every key. The public API can create work; it cannot destroy a tenant.</para>
    /// </summary>
    public static IEndpointRouteBuilder MapDemoEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/settings/demo").WithTags("Demo").RequireAuthorization(ApiPolicies.Admin);

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new GetDemoStatusQuery(), cancellationToken)))
            .WithSummary("Whether this install holds demo data, whether that can be changed here, and what the confirmation must say.")
            .Produces<DemoStatusDto>();

        group.MapPost("/", async (DemoModeCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command, cancellationToken)))
            .WithSummary(
                "Turns demo mode on or off. BOTH DIRECTIONS DELETE EVERY WORK ITEM, RESPONSIBILITY, ENTITY AND USER " +
                "IN THE TENANT except the caller, and cannot be undone. Requires the workspace name typed out and the " +
                "caller's own password. 404 when Demo:AllowReset is off.")
            .Produces<DemoModeResultDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
