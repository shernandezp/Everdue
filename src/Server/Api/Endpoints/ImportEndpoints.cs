using System.Text.Json;
using Common.Mediator;
using Everdue.Server.Application.Common;
using Everdue.Server.Application.Imports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Api.Endpoints;

public static class ImportEndpoints
{
    /// <summary>
    /// Two steps, and no server-side intermediate state: the commit re-posts the file with the confirmed mapping.
    /// The alternative — a temp file, a token table, an expiry sweeper and a leak when somebody closes the tab —
    /// is a whole subsystem for a 200 KB spreadsheet.
    ///
    /// Administrator-only, and deliberately not reachable with an API key: bulk-creating rows from a file is a
    /// human decision made in front of a preview.
    /// </summary>
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/imports")
            .WithTags("Imports")
            .RequireAuthorization(ApiPolicies.Admin)
            .DisableAntiforgery();

        group.MapPost("/{kind}/preview", async (
                string kind,
                IFormFile file,
                ISender sender,
                IOptions<ImportOptions> options,
                CancellationToken cancellationToken) =>
            {
                var content = await ReadAsync(file, options.Value, cancellationToken);
                return Results.Ok(await sender.Send(new PreviewImportCommand(ParseKind(kind), content), cancellationToken));
            })
            .WithSummary("Parses and validates without writing anything. Returns a suggested column mapping.")
            .Produces<ImportPreviewDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{kind}/commit", async (
                string kind,
                IFormFile file,
                [FromForm] string mapping,
                ISender sender,
                IOptions<ImportOptions> options,
                CancellationToken cancellationToken) =>
            {
                var content = await ReadAsync(file, options.Value, cancellationToken);
                return Results.Ok(await sender.Send(
                    new CommitImportCommand(ParseKind(kind), content, ParseMapping(mapping)),
                    cancellationToken));
            })
            .WithSummary("Creates the valid rows, skips duplicates, and reports each failure with its row number.")
            .Produces<ImportResultDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return api;
    }

    private static ImportKind ParseKind(string kind)
        => EnumQuery.Parse<ImportKind>(kind, "kind") ?? throw new ValidationException("An import kind is required.");

    /// <summary>
    /// The mapping travels as a JSON object in a form field, because the request is already multipart for the
    /// file and a second part is cheaper than base64-ing the file into a JSON body.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ParseMapping(string? mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["mapping"] = ["Confirm which column holds which field."],
            });
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(mapping, JsonSerializerOptions.Web)
                   ?? throw new ValidationException("The column mapping could not be read.");
        }
        catch (JsonException)
        {
            throw new ValidationException("The column mapping could not be read.");
        }
    }

    /// <summary>
    /// Checked before the stream is touched. Refusing an oversize file after reading it is not a limit, it is a
    /// delay — the same reasoning the attachment endpoint documents.
    /// </summary>
    private static async Task<byte[]> ReadAsync(IFormFile file, ImportOptions options, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["file"] = ["The file is empty."] });
        }

        if (file.Length > options.MaxSizeBytes)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = [$"The file is larger than the {options.MaxSizeBytes / 1024} KB limit."],
            });
        }

        using var buffer = new MemoryStream();
        await using var stream = file.OpenReadStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }
}
