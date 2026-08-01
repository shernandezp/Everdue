namespace Everdue.Server.Application.Contracts;

public sealed record AttachmentDto(
    Guid Id,
    Guid WorkItemId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedByUserId,
    string UploadedByDisplayName,
    DateTimeOffset CreatedAt);

public sealed record SavedViewDto(Guid Id, string Name, string Route, string QueryString, DateTimeOffset CreatedAt);

/// <summary>
/// Per-item results, because a bulk action over thirty items where two were already completed is a
/// normal Tuesday — not an error, and not a silent partial success either.
/// </summary>
public sealed record BulkItemFailureDto(Guid Id, string Error);

public sealed record BulkResultDto(IReadOnlyList<Guid> Succeeded, IReadOnlyList<BulkItemFailureDto> Failed)
{
    public int Total => Succeeded.Count + Failed.Count;
}

public sealed record ReassignResultDto(int Responsibilities, int WorkItems);

/// <summary>Advertised to the login screen so the Google button only appears where it can work.</summary>
public sealed record AuthProvidersDto(bool Google);
