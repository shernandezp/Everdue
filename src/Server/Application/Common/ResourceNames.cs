namespace Everdue.Server.Application.Common;

/// <summary>
/// What a <see cref="NotFoundException"/> calls the thing it could not find.
///
/// Developer-facing English, like every other API error message — but spelled the same way every
/// time. Twelve handlers raise "not found" for a user, and before this they each spelled it
/// themselves, which is how "Work item" and "WorkItem" end up in the same log.
/// </summary>
public static class ResourceNames
{
    public const string User = "User";
    public const string Tenant = "Tenant";
    public const string Entity = "Entity";
    public const string Department = "Department";
    public const string Responsibility = "Responsibility";
    public const string WorkItem = "Work item";
    public const string Comment = "Comment";
    public const string Attachment = "Attachment";
    public const string AttachmentFile = "Attachment file";
    public const string SavedView = "Saved view";
    public const string ChecklistItem = "Checklist item";
    public const string EntityFieldDef = "Custom field";
    public const string ApiKey = "API key";
    public const string Webhook = "Webhook subscription";

    /// <summary>Raised when Demo:AllowReset is off — the capability is absent from the install, not refused to the caller.</summary>
    public const string DemoMode = "Demo mode";
}
