using System.ComponentModel.DataAnnotations;

namespace Everdue.Server.Application.Insights;

/// <summary>
/// Shape constants for the insight reports — not policy, which is why they are configuration rather
/// than tenant settings. Chronic detection is two integers on purpose: a rules engine would be scope
/// creep for a question that has one honest form ("missed K of the last N").
///
/// Lives in the Application layer for the same reason <see cref="Attachments.AttachmentOptions"/>
/// does: the handlers enforce these, and a handler never reaches into Infrastructure. Binding them to
/// configuration is still Infrastructure's job.
/// </summary>
public sealed class InsightsOptions
{
    public const string Section = "Insights";

    /// <summary>K: misses inside the window that make a responsibility chronically delayed.</summary>
    [Range(1, 100)]
    public int ChronicMissCount { get; set; } = 3;

    /// <summary>N: how many of a responsibility's most recent concluded periods are judged.</summary>
    [Range(1, 500)]
    public int ChronicWindowPeriods { get; set; } = 8;

    /// <summary>Below this many concluded occurrences a percentage is withheld and the raw pair shown.</summary>
    [Range(1, 1000)]
    public int MinOccurrencesForRate { get; set; } = 5;

    /// <summary>Concluded buckets in the default rolling window. The current, partial one is added to it.</summary>
    [Range(1, 520)]
    public int DefaultTrendBuckets { get; set; } = 12;

    /// <summary>Ceiling on any trend axis. A wider calendar range is refused rather than truncated.</summary>
    [Range(1, 520)]
    public int MaxTrendBuckets { get; set; } = 52;

    /// <summary>Rows kept in the by-entity lists; what the cap dropped is always reported, never hidden.</summary>
    [Range(1, 200)]
    public int TopEntities { get; set; } = 15;
}
