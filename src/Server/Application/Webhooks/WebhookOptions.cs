using System.ComponentModel.DataAnnotations;

namespace Everdue.Server.Application.Webhooks;

public sealed class WebhookOptions
{
    public const string Section = "Webhooks";

    public bool Enabled { get; set; } = true;

    /// <summary>How often the outbox is drained.</summary>
    [Range(5, 3600)]
    public int DispatchSeconds { get; set; } = 15;

    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Deliveries pulled per pass.</summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// After this many failures in a row a subscription is disabled and stays disabled until an administrator
    /// re-enables it. An endpoint that has failed ten times running has changed; guessing otherwise just
    /// resumes the noise.
    /// </summary>
    [Range(1, 1000)]
    public int MaxConsecutiveFailures { get; set; } = 10;

    /// <summary>A cap on how many places one tenant fans every event out to.</summary>
    [Range(1, 100)]
    public int MaxSubscriptions { get; set; } = 10;

    /// <summary>
    /// Terminal deliveries are swept after this many days. Failed rows are kept four times as long, because
    /// they are what an administrator debugs with. Deliveries are not the ledger.
    /// </summary>
    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// Allows <c>http://</c> receivers. Off by default; on, it is for a localhost receiver during development.
    /// Private and loopback addresses are allowed either way — posting to an automation box on the same LAN is
    /// the actual self-hosted use case, and an administrator who can add a subscription can already read
    /// everything it would carry.
    /// </summary>
    public bool AllowInsecureUrls { get; set; }

    /// <summary>How long one delivery attempt may take before it counts as a timeout.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 10;
}
