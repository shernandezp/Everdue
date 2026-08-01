using Common.Mediator;
using Everdue.Server.Application.ApiKeys;

namespace Everdue.Server.Api.Endpoints;

public static class ApiKeyEndpoints
{
    /// <summary>
    /// Key management is administrator-only and <strong>never reachable with a key</strong> — no
    /// <c>.AllowApiKey()</c> here. A credential that can mint credentials is a credential that cannot be
    /// contained, and that property is what makes the endpoint allow-list worth having.
    /// </summary>
    public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/api-keys")
            .WithTags("API keys")
            .RequireAuthorization(ApiPolicies.Admin);

        group.MapGet("/", async ([AsParameters] ListApiKeysQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .Produces<IReadOnlyList<ApiKeyDto>>();

        group.MapPost("/", async (CreateApiKeyCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(command, cancellationToken);
                return Results.Created($"/api/v1/api-keys/{created.Key.Id}", created);
            })
            .WithSummary("Returns the token once. Everdue stores only its prefix and a SHA-256 hash and cannot show it again.")
            .Produces<CreatedApiKeyDto>(StatusCodes.Status201Created);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new RevokeApiKeyCommand(id), cancellationToken)))
            .WithSummary("Revokes immediately. The row is kept, because 'this key existed and was withdrawn' is the answer somebody needs.")
            .Produces<ApiKeyDto>();

        return api;
    }
}
