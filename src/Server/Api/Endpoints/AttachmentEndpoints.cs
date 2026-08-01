using Common.Mediator;
using Everdue.Server.Application.Attachments;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Api.Endpoints;

public static class AttachmentEndpoints
{
    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/workitems/{id:guid}/attachments", async (Guid id, ISender sender, CancellationToken cancellationToken)
                => Results.Ok(await sender.Send(new ListAttachmentsQuery(id), cancellationToken)))
            .WithTags("Attachments")
            .RequireAuthorization()
            .AllowApiKey()
            .Produces<IReadOnlyList<AttachmentDto>>();

        api.MapPost("/workitems/{id:guid}/attachments", UploadAsync)
            .WithTags("Attachments")
            .RequireAuthorization()
            .AllowApiKey()
            .WithSummary("One file per request. Size and type are enforced server-side.")
            .DisableAntiforgery()
            .Produces<AttachmentDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        var single = api.MapGroup("/attachments").WithTags("Attachments").RequireAuthorization().AllowApiKey();

        single.MapGet("/{id:guid}", DownloadAsync)
            .WithSummary("Streams the file to an authenticated caller. Never served as a static file.");

        single.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteAttachmentCommand(id), cancellationToken);
                return Results.NoContent();
            })
            .WithSummary("Uploader or administrator only.");

        return api;
    }

    private static async Task<IResult> UploadAsync(
        Guid id,
        IFormFile file,
        ISender sender,
        IOptions<AttachmentOptions> options,
        CancellationToken cancellationToken)
    {
        // Checked before the stream is touched: refusing a 200 MB upload after reading it is not a
        // limit, it is a delay.
        if (file.Length > options.Value.MaxSizeBytes)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = [$"The file is larger than the {options.Value.MaxSizeBytes / (1024 * 1024)} MB limit."],
            });
        }

        await using var stream = file.OpenReadStream();

        var created = await sender.Send(
            new UploadAttachmentCommand(id, file.FileName, file.ContentType, file.Length, stream),
            cancellationToken);

        return Results.Created($"/api/v1/attachments/{created.Id}", created);
    }

    private static async Task<IResult> DownloadAsync(Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var download = await sender.Send(new DownloadAttachmentQuery(id), cancellationToken);

        // Always an attachment, never inline: nothing uploaded by a user should be rendered by the
        // browser in this origin. Private and unstored because it is somebody's work, not an asset.
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.ContentDisposition = ContentDispositionFor(download.FileName);

        return Results.Stream(download.Content, download.ContentType);
    }

    /// <summary>
    /// RFC 6266/5987: an ASCII <c>filename</c> for old clients and a percent-encoded
    /// <c>filename*</c> that carries the real one. A product shipped in Spanish will meet
    /// "Guía de recepción.pdf" on its first day, and the plain parameter cannot express it.
    /// </summary>
    private static string ContentDispositionFor(string fileName)
    {
        var ascii = new string(fileName.Select(c => c is >= ' ' and <= '~' && c != '"' ? c : '_').ToArray());
        var encoded = Uri.EscapeDataString(fileName);

        return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{encoded}";
    }
}
