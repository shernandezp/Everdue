using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Webhooks;

internal static class WebhookMapping
{
    public static WebhookSubscriptionDto ToDto(WebhookSubscription subscription)
        => new(
            subscription.Id,
            subscription.Url,
            subscription.SubscribedTypes(),
            subscription.Active,
            subscription.ConsecutiveFailures,
            subscription.DisabledAt,
            subscription.LastSuccessAt,
            subscription.LastError,
            subscription.CreatedAt);

    /// <summary>
    /// Ping is not subscribable — it is what the test button sends. Accepting it in a subscription would mean
    /// promising to deliver an event nothing raises.
    /// </summary>
    public static string ValidateTypes(IReadOnlyList<WebhookEventType> types)
    {
        var cleaned = types.Distinct().Where(t => WebhookEvents.Subscribable.Contains(t)).ToArray();

        if (cleaned.Length == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["eventTypes"] = [$"Choose at least one event. Available: {string.Join(", ", WebhookEvents.Subscribable.Select(WebhookEvents.WireName))}."],
            });
        }

        return WebhookEvents.FormatTypes(cleaned);
    }
}

public sealed class ListWebhooksHandler(IEverdueDbContext db)
    : IRequestHandler<ListWebhooksQuery, IReadOnlyList<WebhookSubscriptionDto>>
{
    public async Task<IReadOnlyList<WebhookSubscriptionDto>> Handle(
        ListWebhooksQuery request,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.WebhookSubscriptions.AsNoTracking()
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(WebhookMapping.ToDto).ToArray();
    }
}

public sealed class CreateWebhookHandler(
    IEverdueDbContext db,
    IWebhookAdminSupport support,
    ICurrentUser currentUser,
    IClock clock,
    IOptions<WebhookOptions> options) : IRequestHandler<CreateWebhookCommand, CreatedWebhookDto>
{
    public async Task<CreatedWebhookDto> Handle(CreateWebhookCommand request, CancellationToken cancellationToken = default)
    {
        support.ValidateUrl(request.Url);

        var max = options.Value.MaxSubscriptions;

        if (await db.WebhookSubscriptions.CountAsync(cancellationToken) >= max)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["url"] = [$"There are already {max} webhook subscriptions, which is the maximum."],
            });
        }

        var (secret, protectedSecret) = support.NewSecret();

        var subscription = new WebhookSubscription
        {
            Id = Guid.CreateVersion7(),
            Url = request.Url.Trim(),
            SecretProtected = protectedSecret,
            EventTypes = WebhookMapping.ValidateTypes(request.EventTypes),
            Active = true,
            CreatedByUserId = currentUser.RequireUserId(),
            CreatedAt = clock.UtcNow,
        };

        db.WebhookSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);

        // The only time the secret exists outside the key ring.
        return new CreatedWebhookDto(WebhookMapping.ToDto(subscription), secret);
    }
}

public sealed class UpdateWebhookHandler(IEverdueDbContext db, IWebhookAdminSupport support)
    : IRequestHandler<UpdateWebhookCommand, WebhookSubscriptionDto>
{
    public async Task<WebhookSubscriptionDto> Handle(UpdateWebhookCommand request, CancellationToken cancellationToken = default)
    {
        var subscription = await db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
                           ?? throw new NotFoundException(ResourceNames.Webhook, request.Id);

        support.ValidateUrl(request.Url);

        subscription.Url = request.Url.Trim();
        subscription.EventTypes = WebhookMapping.ValidateTypes(request.EventTypes);

        // Re-enabling is what brings an auto-disabled subscription back, and it starts the failure count over:
        // otherwise the eleventh failure ever would disable it again immediately.
        if (request.Active && !subscription.Active)
        {
            subscription.ConsecutiveFailures = 0;
            subscription.DisabledAt = null;
            subscription.LastError = null;
        }

        subscription.Active = request.Active;

        await db.SaveChangesAsync(cancellationToken);
        return WebhookMapping.ToDto(subscription);
    }
}

public sealed class DeleteWebhookHandler(IEverdueDbContext db) : IRequestHandler<DeleteWebhookCommand, bool>
{
    public async Task<bool> Handle(DeleteWebhookCommand request, CancellationToken cancellationToken = default)
    {
        var subscription = await db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
                           ?? throw new NotFoundException(ResourceNames.Webhook, request.Id);

        // Its pending and historical deliveries go with it: they are addressed to a receiver that no longer
        // exists, and keeping them would leave the health screen reporting on nothing.
        db.WebhookSubscriptions.Remove(subscription);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed class TestWebhookHandler(IEverdueDbContext db, IWebhookAdminSupport support)
    : IRequestHandler<TestWebhookCommand, WebhookSubscriptionDto>
{
    public async Task<WebhookSubscriptionDto> Handle(TestWebhookCommand request, CancellationToken cancellationToken = default)
    {
        var subscription = await db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
                           ?? throw new NotFoundException(ResourceNames.Webhook, request.Id);

        if (!subscription.Active)
        {
            throw new ConflictException("This subscription is disabled. Re-enable it before testing.");
        }

        support.EnqueuePing(subscription);
        await db.SaveChangesAsync(cancellationToken);

        return WebhookMapping.ToDto(subscription);
    }
}
