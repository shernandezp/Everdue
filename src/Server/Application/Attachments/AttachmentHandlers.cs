using Common.Mediator;
using Everdue.Server.Application.Abstractions;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Everdue.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Attachments;

public sealed class ListAttachmentsHandler(IEverdueDbContext db, IUserDirectory users)
    : IRequestHandler<ListAttachmentsQuery, IReadOnlyList<AttachmentDto>>
{
    public async Task<IReadOnlyList<AttachmentDto>> Handle(ListAttachmentsQuery request, CancellationToken cancellationToken = default)
    {
        var rows = await db.Attachments.AsNoTracking()
            .Where(a => a.WorkItemId == request.WorkItemId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.WorkItemId,
                a.FileName,
                a.ContentType,
                a.SizeBytes,
                a.UploadedByUserId,
                a.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var directory = await users.MapAsync(rows.Select(r => r.UploadedByUserId), cancellationToken);

        return rows
            .Select(r => new AttachmentDto(
                r.Id,
                r.WorkItemId,
                r.FileName,
                r.ContentType,
                r.SizeBytes,
                r.UploadedByUserId,
                directory.TryGetValue(r.UploadedByUserId, out var user) ? user.DisplayName : "—",
                r.CreatedAt))
            .ToArray();
    }
}

/// <summary>
/// Validates before it writes anything: an oversize or disallowed file must never reach the disk,
/// because "clean it up afterwards" is the step that gets skipped when something throws in between.
/// </summary>
public sealed class UploadAttachmentHandler(
    IEverdueDbContext db,
    IFileStore files,
    ICurrentUser currentUser,
    IUserDirectory users,
    ITenantContext tenantContext,
    IClock clock,
    IOptions<AttachmentOptions> options) : IRequestHandler<UploadAttachmentCommand, AttachmentDto>
{
    public async Task<AttachmentDto> Handle(UploadAttachmentCommand request, CancellationToken cancellationToken = default)
    {
        var limits = options.Value;

        if (!await db.WorkItems.AnyAsync(w => w.Id == request.WorkItemId, cancellationToken))
        {
            throw new NotFoundException(ResourceNames.WorkItem, request.WorkItemId);
        }

        // Reduced to its leaf and stripped of control characters. The leaf because a path is not a
        // name; the control characters because this string ends up in a response header, and a
        // newline in a header is somebody else's exploit.
        var fileName = new string(
            Path.GetFileName(request.FileName).Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["file"] = ["A file name is required."] });
        }

        if (request.Length <= 0)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["file"] = ["The file is empty."] });
        }

        if (request.Length > limits.MaxSizeBytes)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = [$"The file is larger than the {limits.MaxSizeBytes / (1024 * 1024)} MB limit."],
            });
        }

        var contentType = (request.ContentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // Both are caller-controlled, so both have to agree with the allow-list. This is not content
        // sniffing and does not pretend to be — scanning is a hosted-version concern.
        if (!limits.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)
            || !limits.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = [$"Files of type '{contentType}' are not allowed. Allowed: {string.Join(", ", limits.AllowedContentTypes)}."],
            });
        }

        var existing = await db.Attachments.CountAsync(a => a.WorkItemId == request.WorkItemId, cancellationToken);
        if (existing >= limits.MaxPerWorkItem)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = [$"This item already has the maximum of {limits.MaxPerWorkItem} attachments."],
            });
        }

        var userId = currentUser.RequireUserId();
        var id = Guid.CreateVersion7();

        var attachment = new Attachment
        {
            Id = id,
            WorkItemId = request.WorkItemId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = request.Length,

            // Never the uploaded name: path traversal is impossible by construction, not by sanitising.
            StorageKey = Attachment.KeyFor(tenantContext.TenantId, id),
            UploadedByUserId = userId,
            CreatedAt = clock.UtcNow,
        };

        await files.SaveAsync(attachment.StorageKey, request.Content, cancellationToken);

        try
        {
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The bytes are already on disk. A row that never saved would leave them there with
            // nothing pointing at them and no way to find them again — so they go back out.
            await files.DeleteAsync(attachment.StorageKey, CancellationToken.None);
            throw;
        }

        var uploader = await users.FindAsync(userId, cancellationToken);

        return new AttachmentDto(
            attachment.Id,
            attachment.WorkItemId,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            userId,
            uploader?.DisplayName ?? "—",
            attachment.CreatedAt);
    }
}

public sealed class DownloadAttachmentHandler(IEverdueDbContext db, IFileStore files)
    : IRequestHandler<DownloadAttachmentQuery, AttachmentDownload>
{
    public async Task<AttachmentDownload> Handle(DownloadAttachmentQuery request, CancellationToken cancellationToken = default)
    {
        // Tenant-filtered like everything else, so another tenant's id is a 404 rather than a leak.
        var attachment = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException(ResourceNames.Attachment, request.Id);

        var content = await files.OpenReadAsync(attachment.StorageKey, cancellationToken)
                      ?? throw new NotFoundException(ResourceNames.AttachmentFile, request.Id);

        return new AttachmentDownload(attachment.FileName, attachment.ContentType, content);
    }
}

public sealed class DeleteAttachmentHandler(IEverdueDbContext db, IFileStore files, ICurrentUser currentUser, ILogger<DeleteAttachmentHandler> logger)
    : IRequestHandler<DeleteAttachmentCommand, bool>
{
    public async Task<bool> Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken = default)
    {
        var attachment = await db.Attachments.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException(ResourceNames.Attachment, request.Id);

        if (!currentUser.IsAdmin && attachment.UploadedByUserId != currentUser.RequireUserId())
        {
            throw new ForbiddenException("Only the uploader or an administrator can delete this attachment.");
        }

        try
        {
            await files.DeleteAsync(attachment.StorageKey, cancellationToken);
        }
        catch (IOException e)
        {
            // A record pointing at bytes that are gone is worse than an orphaned file: the row is
            // what the UI shows, so it goes either way.
            logger.LogWarning(e, "Could not delete attachment file {Key}; removing the record anyway.", attachment.StorageKey);
        }

        db.Attachments.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
