using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Everdue.Server.Infrastructure.Persistence;

/// <summary>
/// Every instant in Everdue is UTC. On PostgreSQL that is <c>timestamptz</c>, which rejects a
/// non-zero offset anyway; this converter simply guarantees it.
/// </summary>
public sealed class UtcDateTimeOffsetConverter()
    : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v.ToUniversalTime());

/// <summary>
/// SQLite has no date type and its provider refuses to ORDER BY or aggregate a DateTimeOffset,
/// because the default TEXT encoding carries a per-row offset and would sort wrongly across zones.
/// Storing a fixed-width UTC ISO-8601 string removes the ambiguity: lexicographic order is
/// chronological order, MAX() means what it says, and the file stays readable with any SQLite
/// browser. The Postgres model is untouched — this is the one place the two providers differ.
/// </summary>
public sealed class SqliteDateTimeOffsetConverter()
    : ValueConverter<DateTimeOffset, string>(
        v => v.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture),
        v => DateTimeOffset.ParseExact(v, Format, CultureInfo.InvariantCulture, Styles))
{
    internal const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    internal const DateTimeStyles Styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
}
