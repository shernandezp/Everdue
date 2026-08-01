using System.ComponentModel.DataAnnotations;

namespace Everdue.Server.Application.Imports;

public sealed class ImportOptions
{
    public const string Section = "Import";

    /// <summary>Rows in one file. A client list is hundreds; five thousand is generous and still bounded.</summary>
    [Range(1, 100_000)]
    public int MaxRows { get; set; } = 5_000;

    /// <summary>
    /// Bytes. Small on purpose — this is a spreadsheet of references, not a document store. The API's
    /// request-size ceiling is the larger of this and the attachment limit, so neither silently decides the
    /// other's maximum.
    /// </summary>
    [Range(1024, 100 * 1024 * 1024)]
    public long MaxSizeBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>How many parsed rows the preview shows before anything is written.</summary>
    [Range(1, 200)]
    public int PreviewRows { get; set; } = 20;

    /// <summary>
    /// How many individual row failures the commit response carries. The counts are always exact; only the
    /// per-row list is bounded, because a response is not a log file.
    /// </summary>
    [Range(10, 10_000)]
    public int MaxReportedFailures { get; set; } = 1_000;
}
