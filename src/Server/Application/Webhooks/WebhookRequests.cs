using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Domain;

namespace Everdue.Server.Application.Webhooks;

/// <summary>
/// What the admin screen shows. The signing secret is absent by construction: there is no field for it, which
/// is the only reliable way to guarantee it is never returned.
/// </summary>
public sealed record WebhookSubscriptionDto(
    Guid Id,
    string Url,
    IReadOnlyList<WebhookEventType> EventTypes,
    bool Active,
    int ConsecutiveFailures,
    DateTimeOffset? DisabledAt,
    DateTimeOffset? LastSuccessAt,
    string? LastError,
    DateTimeOffset CreatedAt);

/// <summary>Creation returns the secret once. It cannot be shown again — only its ciphertext is stored.</summary>
public sealed record CreatedWebhookDto(WebhookSubscriptionDto Subscription, string Secret);

/// <summary>
/// Derived from the deliveries table rather than from counter columns, exactly as channel health is: the rows
/// already hold the answer, and a counter is a second copy of a fact that can drift.
/// </summary>
public sealed record WebhookHealthDto(
    Guid SubscriptionId,
    string Url,
    bool Active,
    int Pending,
    int Failed24h,
    int Sent24h,
    DateTimeOffset? LastSuccessAt,
    string? LastError);

public sealed record ListWebhooksQuery : IQuery<IReadOnlyList<WebhookSubscriptionDto>>;

public sealed record CreateWebhookCommand(
    [property: Required, MaxLength(500)] string Url,
    IReadOnlyList<WebhookEventType> EventTypes) : ICommand<CreatedWebhookDto>;

/// <summary>
/// Also the way an auto-disabled subscription comes back: sending <c>active: true</c> clears
/// <c>DisabledAt</c> and resets the failure count. Re-enabling is deliberately a decision somebody makes.
/// </summary>
public sealed record UpdateWebhookCommand(
    Guid Id,
    [property: Required, MaxLength(500)] string Url,
    IReadOnlyList<WebhookEventType> EventTypes,
    bool Active) : ICommand<WebhookSubscriptionDto>;

public sealed record DeleteWebhookCommand(Guid Id) : ICommand<bool>;

public sealed record TestWebhookCommand(Guid Id) : ICommand<WebhookSubscriptionDto>;

public sealed record WebhookHealthQuery : IQuery<IReadOnlyList<WebhookHealthDto>>;
