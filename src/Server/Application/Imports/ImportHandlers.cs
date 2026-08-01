using Common.Mediator;
using Everdue.Server.Application.Common;
using Microsoft.Extensions.Options;

namespace Everdue.Server.Application.Imports;

/// <summary>
/// The two mediator handlers, which do nothing but route: parse the file, resolve the fields for the kind,
/// and hand off to the importer that knows that kind. Both steps read the same field list and the same
/// mapping rules, which is what makes the preview an honest rehearsal of the commit.
/// </summary>
public sealed class PreviewImportHandler(
    EntityImportHandler entities,
    WorkItemImportHandler workItems,
    IOptions<ImportOptions> options) : IRequestHandler<PreviewImportCommand, ImportPreviewDto>
{
    public async Task<ImportPreviewDto> Handle(PreviewImportCommand request, CancellationToken cancellationToken = default)
    {
        var table = CsvSource.Read(request.Content, options.Value.MaxRows);

        var fields = request.Kind == ImportKind.Entities
            ? await entities.FieldsAsync(cancellationToken)
            : WorkItemImportHandler.Fields;

        var aliases = request.Kind == ImportKind.Entities
            ? EntityImportHandler.Aliases
            : WorkItemImportHandler.Aliases;

        var suggestion = ImportMapping.Suggest(table.Headers, fields, aliases);

        // The suggestion is validated as if it had been confirmed, so the preview shows the errors the commit
        // would produce. If the guess is missing a required column the rows are shown unparsed instead.
        var rows = new List<ImportPreviewRowDto>();
        var take = Math.Min(options.Value.PreviewRows, table.Rows.Count);

        ImportMapping? mapping = null;

        try
        {
            mapping = ImportMapping.Resolve(table.Headers, suggestion, fields);
        }
        catch (ValidationException)
        {
            mapping = null;
        }

        if (mapping is not null)
        {
            // Both readers load what the whole file needs once, so a preview costs the same handful of queries
            // whether it shows one row or twenty.
            var lookups = request.Kind == ImportKind.WorkItems
                ? await workItems.LoadLookupsAsync(cancellationToken)
                : null;

            var definitions = request.Kind == ImportKind.Entities
                ? await entities.LoadDefinitionsAsync(cancellationToken)
                : null;

            for (var index = 0; index < take; index++)
            {
                var rowNumber = index + 2;
                var row = table.Rows[index];

                var error = request.Kind == ImportKind.Entities
                    ? entities.Parse(row, mapping, definitions!).Error
                    : workItems.Parse(row, mapping, lookups!).Error;

                rows.Add(new ImportPreviewRowDto(rowNumber, Values(fields, mapping, row), error));
            }
        }
        else
        {
            for (var index = 0; index < take; index++)
            {
                rows.Add(new ImportPreviewRowDto(index + 2, new Dictionary<string, string?>(), null));
            }
        }

        return new ImportPreviewDto(
            request.Kind,
            table.Delimiter,
            table.Encoding,
            table.Rows.Count,
            table.Headers,
            fields,
            suggestion,
            rows);
    }

    private static Dictionary<string, string?> Values(
        IReadOnlyList<ImportFieldDto> fields,
        ImportMapping mapping,
        string[] row)
        => fields.ToDictionary(field => field.Key, field => mapping.Value(row, field.Key));
}

public sealed class CommitImportHandler(
    EntityImportHandler entities,
    WorkItemImportHandler workItems,
    IOptions<ImportOptions> options) : IRequestHandler<CommitImportCommand, ImportResultDto>
{
    public async Task<ImportResultDto> Handle(CommitImportCommand request, CancellationToken cancellationToken = default)
    {
        var table = CsvSource.Read(request.Content, options.Value.MaxRows);

        var fields = request.Kind == ImportKind.Entities
            ? await entities.FieldsAsync(cancellationToken)
            : WorkItemImportHandler.Fields;

        var mapping = ImportMapping.Resolve(table.Headers, request.Mapping, fields);

        return request.Kind switch
        {
            ImportKind.Entities => await entities.CommitAsync(table, mapping, cancellationToken),
            ImportKind.WorkItems => await workItems.CommitAsync(table, mapping, cancellationToken),
            _ => throw new ValidationException($"'{request.Kind}' cannot be imported."),
        };
    }
}
