using Common.Mediator;
using Everdue.Server.Application.Webhooks;
using Everdue.Server.Domain;

namespace Everdue.Server.Api.Endpoints;

public static class WebhookEndpoints
{
    /// <summary>
    /// Subscription management, administrator-only and not reachable with an API key — for the same reason key
    /// management is not: a credential that can redirect the event stream somewhere else is not containable.
    /// </summary>
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/webhooks")
            .WithTags("Webhooks")
            .RequireAuthorization(ApiPolicies.Admin);

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new ListWebhooksQuery(), cancellationToken)))
            .WithSummary("Subscriptions. The signing secret is never returned — the DTO has no field for it.")
            .Produces<IReadOnlyList<WebhookSubscriptionDto>>();

        group.MapGet("/event-types", (CancellationToken _)
                => Results.Ok(WebhookEvents.Subscribable.Select(type => new
                {
                    value = type.ToString(),
                    name = WebhookEvents.WireName(type),
                })))
            .WithSummary("The six subscribable events, with the names they carry on the wire.");

        group.MapGet("/health", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new WebhookHealthQuery(), cancellationToken)))
            .WithSummary("Pending / failed-in-24h / last error per subscription, derived from the deliveries table.")
            .Produces<IReadOnlyList<WebhookHealthDto>>();

        group.MapPost("/", async (CreateWebhookCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(command, cancellationToken);
                return Results.Created($"/api/v1/webhooks/{created.Subscription.Id}", created);
            })
            .WithSummary("Returns the signing secret once. Verify with it as described in docs/api.md.")
            .Produces<CreatedWebhookDto>(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}", async (Guid id, UpdateWebhookCommand command, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(command with { Id = id }, cancellationToken)))
            .WithSummary("Sending active:true also re-enables an auto-disabled subscription and resets its failure count.")
            .Produces<WebhookSubscriptionDto>();

        group.MapPost("/{id:guid}/test", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new TestWebhookCommand(id), cancellationToken)))
            .WithSummary("Queues one signed `ping`, so a receiver can be proved before it is trusted.")
            .Produces<WebhookSubscriptionDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteWebhookCommand(id), cancellationToken);
                return Results.NoContent();
            });

        return api;
    }
}
