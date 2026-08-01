namespace Everdue.Server.Domain;

/// <summary>
/// One attempt to get one notification onto one channel. A separate row per channel rather than a
/// JSON blob on the notification: an outbox that cannot be indexed is not an outbox, and JSON
/// predicates are not portable across the two providers this project supports.
///
/// The independence is also the failure isolation — one dead channel cannot delay another channel,
/// another user, or any request path.
/// </summary>
public class NotificationDelivery : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    public Guid NotificationId { get; set; }

    public NotificationChannel Channel { get; set; }

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    public int Attempts { get; set; }

    /// <summary>When the dispatcher may next pick this up. Set on creation, pushed out on each retry.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public string? LastError { get; set; }

    public Notification? Notification { get; set; }

    /// <summary>
    /// Exponential, capped at an hour: a provider that is down for a morning should be retried a
    /// handful of times, not hammered, and a message about today's work is worthless tomorrow.
    /// </summary>
    public static TimeSpan BackoffFor(int attempts)
        => TimeSpan.FromMinutes(Math.Min(Math.Pow(2, Math.Clamp(attempts, 0, 6)), 60));
}
