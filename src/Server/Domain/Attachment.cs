namespace Everdue.Server.Domain;

/// <summary>
/// A file hung off a work item. The bytes live in an <c>IFileStore</c>, never in the database — a
/// self-hoster's backup story is "copy the data directory", and a 10 MB row would quietly break the
/// "the whole state is one small file" property that makes SQLite the right default.
/// </summary>
public class Attachment : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    public Guid WorkItemId { get; set; }

    /// <summary>What the user called it. Used in the download header and nowhere else.</summary>
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>
    /// <c>{tenantId}/{attachmentId}</c>. The uploaded filename never reaches the filesystem, so path
    /// traversal is impossible by construction rather than by sanitising a string correctly.
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    public Guid UploadedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public WorkItem? WorkItem { get; set; }

    public static string KeyFor(Guid tenantId, Guid attachmentId) => $"{tenantId}/{attachmentId}";
}
