using System.Text;

namespace Everdue.Server.Application.Exports;

/// <summary>
/// Writes a <see cref="CsvDocument"/> to a stream.
///
/// Two decisions live here rather than in the mappers, so no mapper can forget them:
///
/// <para><strong>UTF-8 with a byte-order mark.</strong> Without it, Excel on a Spanish or
/// Latin-American machine renders "Guía" as mojibake — and opening the file in Excel is the first thing
/// anybody does with an export.</para>
///
/// <para><strong>The formula-injection guard.</strong> A cell beginning <c>=</c>, <c>+</c>, <c>-</c>,
/// <c>@</c>, tab or carriage return is prefixed with a single quote, per OWASP. Everdue's cells hold
/// user-typed titles, entity names and hold-reason text, all of which land in a spreadsheet.</para>
/// </summary>
public static class CsvTextWriter
{
    public const string ContentType = "text/csv; charset=utf-8";

    /// <summary>Characters a spreadsheet treats as the start of a formula.</summary>
    private static readonly char[] FormulaLeaders = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>Anything that would otherwise end the cell, the row, or the quoting.</summary>
    private static readonly System.Buffers.SearchValues<char> NeedsQuoting =
        System.Buffers.SearchValues.Create([',', '"', '\n', '\r', ';']);

    public static async Task WriteAsync(CsvDocument document, Stream destination, CancellationToken cancellationToken)
    {
        // The BOM is written by the encoding, and the writer is left un-disposed of the stream itself:
        // the response body is not ours to close.
        await using var writer = new StreamWriter(
            destination,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            bufferSize: 16 * 1024,
            leaveOpen: true);

        await writer.WriteLineAsync(Row(document.Headers.Select(h => (string?)h)));

        await foreach (var row in document.Rows.WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(Row(row));
        }

        await writer.FlushAsync(cancellationToken);
    }

    private static string Row(IEnumerable<string?> cells)
        => string.Join(',', cells.Select(Cell));

    private static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var guarded = FormulaLeaders.Contains(value[0]) ? "'" + value : value;

        return guarded.AsSpan().IndexOfAny(NeedsQuoting) >= 0
            ? $"\"{guarded.Replace("\"", "\"\"")}\""
            : guarded;
    }
}
