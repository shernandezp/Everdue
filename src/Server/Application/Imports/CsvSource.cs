using System.Text;
using Everdue.Server.Application.Common;
using Sylvan.Data.Csv;

namespace Everdue.Server.Application.Imports;

/// <summary>A parsed file: its header names and its data rows, each row as an array aligned to the header.</summary>
public sealed record CsvTable(
    IReadOnlyList<string> Headers,
    IReadOnlyList<string[]> Rows,
    char Delimiter,
    string Encoding);

/// <summary>
/// Reading a CSV somebody exported from a spreadsheet.
///
/// Two things here are not incidental. <strong>The delimiter is detected</strong> between <c>,</c> and
/// <c>;</c>, because Spanish-locale Excel writes semicolons and a Spanish-first product that cannot read
/// Spanish Excel's CSV is not importable. <strong>The encoding is detected from the byte-order mark</strong>,
/// falling back to UTF-8, which is what a file that has been through Excel and back actually looks like.
///
/// Quoting, embedded newlines and ragged rows are the library's problem, deliberately: those are the edge
/// cases hand-rolled CSV readers get wrong.
/// </summary>
public static class CsvSource
{
    public static CsvTable Read(byte[] content, int maxRows)
    {
        if (content.Length == 0)
        {
            throw new ValidationException("The file is empty.");
        }

        var encoding = DetectEncoding(content, out var preamble);
        var text = encoding.GetString(content, preamble, content.Length - preamble);

        var delimiter = DetectDelimiter(text);

        using var reader = CsvDataReader.Create(
            new StringReader(text),
            new CsvDataReaderOptions { Delimiter = delimiter, HasHeaders = true });

        var headers = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetName(i)?.Trim() ?? string.Empty)
            .ToArray();

        if (headers.Length == 0 || headers.All(string.IsNullOrWhiteSpace))
        {
            throw new ValidationException("The first row must be a header row naming the columns.");
        }

        var rows = new List<string[]>();

        while (reader.Read())
        {
            if (rows.Count >= maxRows)
            {
                throw new ValidationException(
                    $"The file has more than {maxRows:N0} rows. Split it and import the parts separately.");
            }

            var row = new string[headers.Length];

            for (var i = 0; i < headers.Length; i++)
            {
                row[i] = i < reader.FieldCount ? reader.GetString(i).Trim() : string.Empty;
            }

            // A trailing blank line is not a row somebody meant to import.
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(row);
        }

        return new CsvTable(headers, rows, delimiter, encoding.WebName);
    }

    private static Encoding DetectEncoding(byte[] content, out int preambleLength)
    {
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
        {
            preambleLength = 3;
            return new UTF8Encoding(false);
        }

        if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
        {
            preambleLength = 2;
            return Encoding.Unicode;
        }

        if (content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF)
        {
            preambleLength = 2;
            return Encoding.BigEndianUnicode;
        }

        preambleLength = 0;
        return new UTF8Encoding(false);
    }

    /// <summary>
    /// Counted on the header line only. Data can legitimately contain either character inside a quoted
    /// cell; the header is where the file declares its own shape.
    /// </summary>
    private static char DetectDelimiter(string text)
    {
        var newline = text.IndexOfAny(['\r', '\n']);
        var header = newline < 0 ? text : text[..newline];

        return header.Count(c => c == ';') > header.Count(c => c == ',') ? ';' : ',';
    }
}
