using System.ComponentModel.DataAnnotations;
using Common.Mediator;
using Everdue.Server.Application.Contracts;

namespace Everdue.Server.Application.Tenants;

public sealed record GetTenantSettingsQuery : IQuery<TenantSettingsDto>;

public sealed record UpdateTenantSettingsCommand(
    [property: Required, MaxLength(200)] string Name,
    [property: Required, MaxLength(100)] string TimeZoneId,
    [property: Range(0, 23)] int DigestHourLocal,
    [property: Required] string DefaultLanguage,

    /// <summary>
    /// When the "due today" reminders go out. Defaults later than the digest: managers read before
    /// the day starts, the people doing the work want it once they have.
    /// </summary>
    [property: Range(0, 23)] int ReminderHourLocal = 8,

    /// <summary>
    /// May this tenant fall back to the system's channel credentials? True for self-host, where
    /// "system" and "tenant" are the same operator. The hosted product's free plan turns it off.
    /// </summary>
    bool CanUseSystemChannels = true) : ICommand<TenantSettingsDto>;
