using Common.Mediator;
using Everdue.Server.Application.Channels;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;

namespace Everdue.Server.Api.Endpoints;

/// <summary>
/// The configuration arrives as the channel's own JSON shape. A blank secret means "keep the stored
/// one", which is what lets an administrator change a bot's username without re-typing a token the
/// screen deliberately cannot show them.
/// </summary>
public sealed record SaveChannelRequest(string ConfigJson, bool Active);

public static class ChannelEndpoints
{
    public static IEndpointRouteBuilder MapChannelEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/settings/channels").WithTags("Channels").RequireAuthorization(ApiPolicies.Admin);

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new ListChannelSettingsQuery(), cancellationToken)))
            .WithSummary("Scope, active flag and a redacted summary. Never a secret, not even to an administrator.")
            .Produces<IReadOnlyList<ChannelSettingsDto>>();

        group.MapGet("/health", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new ChannelHealthQuery(), cancellationToken)))
            .WithSummary("Derived from the delivery rows: pending, failed in the last day, and the last error.")
            .Produces<IReadOnlyList<ChannelHealthDto>>();

        group.MapPut("/{channel}", async (string channel, SaveChannelRequest body, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(
                    new SaveChannelSettingsCommand(ParseChannel(channel), body.ConfigJson, body.Active),
                    cancellationToken)))
            .WithSummary("Saves this tenant's credentials for one channel.")
            .Produces<ChannelSettingsDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/{channel}", async (string channel, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteChannelSettingsCommand(ParseChannel(channel)), cancellationToken);
                return Results.NoContent();
            })
            .WithSummary("Removes this tenant's credentials. Any system-scope fallback applies again afterwards.");

        group.MapPost("/{channel}/test", async (string channel, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new TestChannelCommand(ParseChannel(channel)), cancellationToken)))
            .WithSummary("Sends one message to the caller — the only honest way to test a channel.")
            .Produces<ChannelTestResultDto>();

        return api;
    }

    /// <summary>Route values bind case-sensitively, and a hand-typed 'telegram' must not 400.</summary>
    private static NotificationChannel ParseChannel(string value)
        => EnumQuery.Parse<NotificationChannel>(value, "channel")
           ?? throw new ValidationException($"'{value}' is not a known channel.");
}
