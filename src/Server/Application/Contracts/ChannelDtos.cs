using Everdue.Server.Domain;

namespace Everdue.Server.Application.Contracts;

/// <summary>
/// What an administrator may see about a channel's configuration: enough to know it is set up and
/// which account it points at — never a token. Re-entering a secret is cheaper than leaking one, so
/// there is no "reveal" and no round-trip of the stored value.
/// </summary>
/// <param name="RedactedConfigJson">
/// This tenant's stored configuration **with every secret removed**, so the form can be edited
/// without re-typing the parts that are not secret. Null when this tenant has no row of its own —
/// a system-scope configuration is not this tenant's to see or edit.
/// </param>
public sealed record ChannelSettingsDto(
    NotificationChannel Channel,
    bool Configured,
    bool Active,
    bool UsingSystemScope,
    string? Summary,
    string? RedactedConfigJson,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Derived from the delivery rows rather than a counter column: the table already holds the answer,
/// and a counter is one more thing that can disagree with reality.
/// </summary>
public sealed record ChannelHealthDto(
    NotificationChannel Channel,
    bool Configured,
    int Pending,
    int FailedRecently,
    int SkippedRecently,
    string? LastError,
    DateTimeOffset? LastErrorAt,
    bool DeliveryReceiptsSupported);

public sealed record ChannelTestResultDto(bool Sent, string? Error);
