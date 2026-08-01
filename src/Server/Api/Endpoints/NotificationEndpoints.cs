using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Application.Notifications;
using Everdue.Server.Domain;

namespace Everdue.Server.Api.Endpoints;

public sealed record MarkReadRequest(IReadOnlyList<Guid>? Ids);

public sealed record UpdatePreferencesRequest(NotificationChannel? Channel, IReadOnlyDictionary<string, bool>? Types);

public sealed record SaveDigestSubscriptionRequest(
    DigestFrequency Frequency,
    DayOfWeek WeeklyDayOfWeek,
    Guid? DepartmentId,
    bool Active);

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListNotificationsQuery query, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(query, cancellationToken)))
            .WithSummary("The signed-in user's notifications, newest first.")
            .Produces<PagedResult<NotificationDto>>();

        group.MapGet("/unread-count", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new UnreadNotificationCountQuery(), cancellationToken)))
            .WithSummary("One integer, polled by the bell. Cheaper than a socket at this scale.")
            .Produces<UnreadCountDto>();

        group.MapPost("/read", async (MarkReadRequest body, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new MarkNotificationsReadCommand(body.Ids), cancellationToken)))
            .WithSummary("Marks the given notifications read, or all of them when no ids are sent.")
            .Produces<UnreadCountDto>();

        return api;
    }

    /// <summary>Everything about "how do I want to be told", under the caller's own profile.</summary>
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/me").WithTags("Me").RequireAuthorization();

        group.MapGet("/notification-preferences", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new GetNotificationPreferencesQuery(), cancellationToken)))
            .Produces<NotificationPreferencesDto>();

        group.MapPut("/notification-preferences", async (UpdatePreferencesRequest body, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new UpdateNotificationPreferencesCommand(body.Channel, body.Types), cancellationToken)))
            .WithSummary("Choosing a channel you have no address on is refused rather than silently ignored.")
            .Produces<NotificationPreferencesDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/telegram/link", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new StartTelegramLinkCommand(), cancellationToken)))
            .WithSummary("Issues a single-use code and the deep link that carries it to the bot.")
            .Produces<TelegramLinkDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/telegram/link", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new UnlinkTelegramCommand(), cancellationToken)))
            .Produces<NotificationPreferencesDto>();

        return api;
    }

    public static IEndpointRouteBuilder MapDigestSubscriptionEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/digest-subscriptions").WithTags("Digest").RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new ListDigestSubscriptionsQuery(), cancellationToken)))
            .WithSummary("Own subscription; administrators see everyone's, because it is a distribution list.")
            .Produces<IReadOnlyList<DigestSubscriptionDto>>();

        group.MapPut("/", async (SaveDigestSubscriptionRequest body, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(
                    new SaveDigestSubscriptionCommand(body.Frequency, body.WeeklyDayOfWeek, body.DepartmentId, body.Active),
                    cancellationToken)))
            .WithSummary("Upsert: there is exactly one subscription per person.")
            .Produces<DigestSubscriptionDto>();

        return api;
    }
}
