using Everdue.Server.Domain;
using Microsoft.AspNetCore.Identity;

namespace Everdue.Server.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity user plus the extra fields Everdue needs. Deactivated users cannot sign in;
/// their history (work items, comments, events) stays exactly where it is.
/// </summary>
public class AppUser : IdentityUser<Guid>, ITenantOwned
{
    public Guid TenantId { get; set; }

    public UserRole Role { get; set; } = UserRole.Member;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>"es" | "en" | null. Null means "use the tenant default".</summary>
    public string? PreferredLanguage { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>Set on the bootstrap admin and on every admin-issued password reset.</summary>
    public bool MustChangePassword { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Serialized <see cref="NotificationPreferences"/>. Null = the defaults (in-app only).</summary>
    public string? NotificationPreferencesJson { get; set; }

    /// <summary>Set once the user completes the /start link flow. Null = not linked.</summary>
    public long? TelegramChatId { get; set; }

    /// <summary>Short-lived one-time code the user sends to the bot to prove who they are.</summary>
    public string? TelegramLinkCode { get; set; }

    public DateTimeOffset? TelegramLinkCodeExpiresAt { get; set; }

    /// <summary>E.164, administrator-maintained: WhatsApp has no linking flow without a public webhook.</summary>
    public string? WhatsAppPhoneE164 { get; set; }

    /// <summary>The language to render this user's digest e-mail in.</summary>
    public string ResolveLanguage(string tenantDefault) => Languages.Resolve(PreferredLanguage, tenantDefault);
}
