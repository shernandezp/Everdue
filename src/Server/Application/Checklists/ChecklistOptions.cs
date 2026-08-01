using System.ComponentModel.DataAnnotations;

namespace Everdue.Server.Application.Checklists;

/// <summary>
/// Lives in Application rather than Infrastructure because the handlers enforce these values, and a
/// handler never reaches into Infrastructure. Binding them to configuration is still Infrastructure's
/// job — the same split <c>AttachmentOptions</c> and <c>InsightsOptions</c> already use.
/// </summary>
public sealed class ChecklistOptions
{
    public const string Section = "Checklists";

    /// <summary>
    /// A cap, not a target. A hundred-line checklist is a document, and Everdue is not a document
    /// system; fifty is past the point where anybody actually ticks them all.
    /// </summary>
    [Range(1, 200)]
    public int MaxItemsPerTemplate { get; set; } = 50;

    /// <summary>Ad-hoc lines are bounded too, for the same reason and by the same number.</summary>
    [Range(1, 200)]
    public int MaxItemsPerWorkItem { get; set; } = 50;
}
