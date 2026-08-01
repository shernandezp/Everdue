using System.ComponentModel.DataAnnotations;

namespace Everdue.Server.Application.Exports;

public sealed class ExportOptions
{
    public const string Section = "Exports";

    /// <summary>
    /// The ceiling on a report or insight export.
    ///
    /// Passing it is a <strong>400</strong>, never a truncated file: an export that looks complete and
    /// is not is the worst possible input to a decision somebody is about to make. Raw table dumps are
    /// streamed and therefore uncapped — they have no aggregation to be wrong about.
    /// </summary>
    /// <remarks>
    /// The lower bound is 1, not a "sensible minimum": an operator who sets five gets five, and a floor here would
    /// protect nothing while making the refusal path awkward to exercise.
    /// </remarks>
    [Range(1, 1_000_000)]
    public int MaxRows { get; set; } = 50_000;
}
