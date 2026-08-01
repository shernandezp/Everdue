using System.Globalization;

namespace Everdue.Server.Application.Exports;

/// <summary>
/// A CSV file, described rather than built: headers plus a stream of rows. Rows are an
/// <see cref="IAsyncEnumerable{T}"/> so a raw table dump can stream row by row instead of materialising
/// a hundred thousand string arrays in memory, while a report export just yields a list it already has.
/// </summary>
public sealed record CsvDocument(string FileName, IReadOnlyList<string> Headers, IAsyncEnumerable<string?[]> Rows);

/// <summary>Formatting helpers, so every mapper writes the same shapes the same way.</summary>
public static class CsvValue
{
    /// <summary>
    /// Instants go out in round-trip ISO 8601 with their offset. A spreadsheet-friendly local rendering
    /// would lose the offset, and an export is data, not a report.
    /// </summary>
    public static string? Instant(DateTimeOffset? value)
        => value?.ToString("o", CultureInfo.InvariantCulture);

    public static string? Date(DateOnly? value)
        => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string? Number(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    public static string? Decimal(double? value)
        => value?.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>A rate as a percentage with one decimal, or blank when it is suppressed or absent.</summary>
    public static string? Percent(double? rate)
        => rate is null ? null : (rate.Value * 100).ToString("0.0", CultureInfo.InvariantCulture);

    public static string Bool(bool value) => value ? "true" : "false";

    public static string? Enum<TEnum>(TEnum? value) where TEnum : struct, Enum
        => value?.ToString();
}
