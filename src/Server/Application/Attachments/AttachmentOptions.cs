using System.ComponentModel.DataAnnotations;

namespace Everdue.Server.Application.Attachments;

/// <summary>
/// The upload limits. Lives in the Application layer rather than with the infrastructure options
/// because the handler enforces them and the layer rule is that a handler never reaches into
/// Infrastructure — binding it to configuration is still Infrastructure's job.
/// </summary>
public sealed class AttachmentOptions
{
    public const string Section = "Attachments";

    [Range(1024, 1_073_741_824)]
    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024;

    [Range(1, 100)]
    public int MaxPerWorkItem { get; set; } = 10;

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/png",
        "image/jpeg",
        "image/webp",
        "application/pdf",
        "text/plain",
    ];

    /// <summary>Checked as well as the declared type: a caller controls both, so both have to agree.</summary>
    public string[] AllowedExtensions { get; set; } =
    [
        ".png", ".jpg", ".jpeg", ".webp", ".pdf", ".txt",
    ];
}
