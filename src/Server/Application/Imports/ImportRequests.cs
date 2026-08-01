using Common.Mediator;

namespace Everdue.Server.Application.Imports;

/// <summary>
/// What can be imported. Responsibilities are deliberately absent: a recurrence rule is four coupled fields
/// with clamping semantics, and expressing it in a spreadsheet cell produces an error report longer than the
/// file. Occurrences can never be imported — the engine creates them and nothing else does.
/// </summary>
public enum ImportKind
{
    Entities = 0,
    WorkItems = 1,
}

/// <summary>One target a spreadsheet column can be mapped onto.</summary>
public sealed record ImportFieldDto(string Key, string Label, bool Required, string? Hint);

/// <summary>
/// What the preview shows: what was detected, what we think each column is, and how the first rows parse —
/// all without writing anything.
/// </summary>
public sealed record ImportPreviewDto(
    ImportKind Kind,
    char Delimiter,
    string Encoding,
    int TotalRows,
    IReadOnlyList<string> Headers,
    IReadOnlyList<ImportFieldDto> Fields,

    /// <summary>Field key → header name, guessed by matching the header against field names and their labels.</summary>
    IReadOnlyDictionary<string, string> SuggestedMapping,
    IReadOnlyList<ImportPreviewRowDto> Rows);

public sealed record ImportPreviewRowDto(int RowNumber, IReadOnlyDictionary<string, string?> Values, string? Error);

public sealed record ImportRowFailureDto(int RowNumber, string Message);

/// <summary>
/// The outcome. Counts are always exact; the per-row failure list is bounded, because a response is not a
/// log file. <paramref name="Skipped"/> is the duplicate count — an import creates or skips, never updates.
/// </summary>
public sealed record ImportResultDto(
    int Created,
    int Skipped,
    int Failed,
    IReadOnlyList<ImportRowFailureDto> Failures);

/// <summary>
/// Step one. Parses and validates without writing anything, so the mapping can be confirmed against real
/// rows rather than guessed at.
/// </summary>
public sealed record PreviewImportCommand(ImportKind Kind, byte[] Content) : ICommand<ImportPreviewDto>;

/// <summary>
/// Step two, with the file sent again.
///
/// There is deliberately no server-side intermediate state. The alternative — a temp file, a token table, an
/// expiry sweeper and a leak when somebody closes the tab — is a whole subsystem for a 200 KB spreadsheet.
/// Re-posting is one extra upload of a small file and removes all of it.
/// </summary>
public sealed record CommitImportCommand(
    ImportKind Kind,
    byte[] Content,
    IReadOnlyDictionary<string, string> Mapping) : ICommand<ImportResultDto>;
