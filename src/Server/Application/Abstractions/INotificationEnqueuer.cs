using Everdue.Server.Domain;

namespace Everdue.Server.Application.Abstractions;

/// <summary>
/// One thing to tell one person. <paramref name="Data"/> is render parameters only — never rendered
/// text, because the same notification is read in the app in one language and delivered to a phone
/// in another.
/// </summary>
public sealed record NotificationRequest(
    Guid UserId,
    NotificationType Type,
    Guid? WorkItemId = null,
    Guid? CommentId = null,
    IReadOnlyDictionary<string, string?>? Data = null,
    string? DedupeKey = null);

/// <summary>
/// The single way anything in the system tells somebody about something.
///
/// Implementations **add rows to the change tracker and do not save**: the notification lands in the
/// same transaction as the change it describes, so a delivery can never exist for work that rolled
/// back. The caller decides when to save — which also lets the occurrence engine keep the ledger
/// write and the notification write apart, since nothing may ever endanger the ledger.
/// </summary>
public interface INotificationEnqueuer
{
    Task EnqueueAsync(NotificationRequest request, CancellationToken cancellationToken = default);

    Task EnqueueManyAsync(IReadOnlyCollection<NotificationRequest> requests, CancellationToken cancellationToken = default);
}

/// <summary>Helper for building the parameter bag without a dictionary literal at every call site.</summary>
public static class NotificationData
{
    public const string Title = "title";
    public const string Entity = "entity";
    public const string DueDate = "dueDate";
    public const string Actor = "actor";
    public const string Reason = "reason";

    public static Dictionary<string, string?> For(params (string Key, string? Value)[] parts)
        => parts.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value);
}
