using System.ComponentModel.DataAnnotations;
using Everdue.Server.Domain;

namespace Everdue.Server.Infrastructure.Options;

public enum DatabaseProvider
{
    Sqlite = 0,
    Postgres = 1,
}

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>Apply pending migrations on startup. Always true for a self-hosted install; tests turn it off.</summary>
    public bool MigrateOnStartup { get; set; } = true;
}

public sealed class EngineOptions
{
    public const string Section = "Engine";

    [Range(1, 1440)]
    public int TickMinutes { get; set; } = 5;

    /// <summary>Safety bound on how many occurrences one responsibility may spawn in a single tick.</summary>
    [Range(1, 100_000)]
    public int MaxOccurrencesPerResponsibilityPerTick { get; set; } = 5_000;

    public bool Enabled { get; set; } = true;
}

public sealed class DigestOptions
{
    public const string Section = "Digest";

    public bool Enabled { get; set; } = true;

    /// <summary>How often the digest service checks whether the tenant's local digest hour has arrived.</summary>
    [Range(1, 240)]
    public int CheckMinutes { get; set; } = 10;
}

public sealed class NotificationOptions
{
    public const string Section = "Notifications";

    public bool Enabled { get; set; } = true;

    /// <summary>How often the outbox is drained. Seconds, because "the same day" is not good enough for a miss.</summary>
    [Range(5, 3600)]
    public int DispatchSeconds { get; set; } = 30;

    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Read notifications older than this are swept. They are not the ledger — WorkItemEvents is.</summary>
    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 90;

    /// <summary>Deliveries pulled per pass. Also the pacing bound: the dispatcher sleeps between sends.</summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Misses older than this are recorded but not announced. Without it, a fortnight of downtime
    /// would flip hundreds of occurrences on one tick and send every one of them.
    /// </summary>
    [Range(1, 168)]
    public int MissedNotificationWindowHours { get; set; } = 24;
}

public sealed class ReminderOptions
{
    public const string Section = "Reminders";

    public bool Enabled { get; set; } = true;

    /// <summary>How often to check whether the tenant's local reminder hour has arrived.</summary>
    [Range(1, 240)]
    public int CheckMinutes { get; set; } = 10;
}

public sealed class TelegramOptions
{
    public const string Section = "Telegram";

    /// <summary>
    /// Long polling is what makes Telegram work for a self-hoster behind NAT — no inbound HTTPS
    /// endpoint required. Turn it off on an install that only ever sends.
    /// </summary>
    public bool PollingEnabled { get; set; } = true;

    [Range(1, 60)]
    public int PollTimeoutSeconds { get; set; } = 30;
}

public sealed class AppOptions
{
    public const string Section = "App";

    /// <summary>
    /// Where this install is reachable, e.g. <c>https://everdue.example.com</c>. Used to put a link in
    /// a notification. Empty is fine — the message then simply carries no link, which is better than
    /// carrying a broken one.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}

/// <summary>Google sign-in. Empty client id = the button is not rendered and the routes 404.</summary>
public sealed class GoogleAuthOptions
{
    public const string Section = "Auth:Google";

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public string? User { get; set; }

    public string? Password { get; set; }

    public string? From { get; set; }

    public string? FromName { get; set; } = "Everdue";

    public bool UseStartTls { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

public sealed class BootstrapOptions
{
    public const string Section = "Bootstrap";

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public string AdminDisplayName { get; set; } = "Administrator";
}

public sealed class SecurityOptions
{
    public const string Section = "Security";

    /// <summary>
    /// Marks the auth cookie <c>Secure</c> unconditionally, so the browser will only ever send it
    /// over HTTPS. Off by default because self-hosters routinely run Everdue on plain HTTP behind
    /// their own reverse proxy, and a cookie the browser refuses to send looks exactly like a
    /// broken password. Turn it on for any install that terminates TLS at the app.
    /// </summary>
    public bool RequireHttps { get; set; }

    /// <summary>
    /// Sign-in attempts allowed per minute, per client address. Defence in depth against password
    /// spraying, which account lockout cannot see — an automated spray makes hundreds of attempts a
    /// minute, a whole office arriving at 09:00 makes a handful. Raise it if an install sits behind
    /// a proxy that collapses everyone onto one address and legitimate users are being turned away.
    /// </summary>
    [Range(5, 10_000)]
    public int LoginAttemptsPerMinute { get; set; } = 30;

    /// <summary>
    /// Requests allowed per minute per API key. Generous enough for an automation platform polling every few
    /// seconds, low enough that a runaway script hits a wall instead of the database. Cookie sessions are
    /// unaffected — the partition returns no limiter for them.
    /// </summary>
    [Range(10, 1_000_000)]
    public int ApiRequestsPerMinute { get; set; } = 600;
}

/// <summary>Seed values for the single configured tenant. Only applied when the tenant row does not exist yet.</summary>
public sealed class TenantOptions
{
    public const string Section = "Tenant";

    public string Name { get; set; } = "Everdue";

    public string TimeZoneId { get; set; } = "UTC";

    [Range(0, 23)]
    public int DigestHourLocal { get; set; } = 7;

    public string DefaultLanguage { get; set; } = Languages.Spanish;
}
