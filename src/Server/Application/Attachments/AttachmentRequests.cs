using Common.Mediator;
using Everdue.Server.Application.Contracts;

namespace Everdue.Server.Application.Attachments;

public sealed record ListAttachmentsQuery(Guid WorkItemId) : IQuery<IReadOnlyList<AttachmentDto>>;

/// <summary>
/// The stream is the payload, so it travels on the command rather than being re-read from the
/// request later — the endpoint's job stays "bind and dispatch".
/// </summary>
public sealed record UploadAttachmentCommand(
    Guid WorkItemId,
    string FileName,
    string? ContentType,
    long Length,
    Stream Content) : ICommand<AttachmentDto>;

/// <summary>Metadata plus the bytes. The caller owns disposing the stream.</summary>
public sealed record AttachmentDownload(string FileName, string ContentType, Stream Content);

public sealed record DownloadAttachmentQuery(Guid Id) : IQuery<AttachmentDownload>;

public sealed record DeleteAttachmentCommand(Guid Id) : ICommand<bool>;
