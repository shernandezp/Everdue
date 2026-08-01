namespace Everdue.Server.Domain.Insights;

/// <summary>
/// One stretch of time a work item spent on hold, reconstructed from the event log.
///
/// Wait time is **calendar** time: nights, weekends and holidays are inside it. Business-hours maths
/// would need a shift and holiday calendar, which is a configuration subsystem for a decimal point —
/// so the screens say "calendar days" instead of implying hours lost.
/// </summary>
public readonly record struct HoldInterval(
    Guid WorkItemId,
    HoldReason Reason,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool Open)
{
    public double Days => (End - Start).TotalDays;

    public bool Overlaps(DateTimeOffset from, DateTimeOffset to) => Start < to && End > from;

    /// <summary>
    /// The part of this interval that falls inside the reporting window, or null if none does. A hold
    /// that started months before the window contributes only the days inside it.
    /// </summary>
    public HoldInterval? Clip(DateTimeOffset from, DateTimeOffset to)
    {
        var start = Start > from ? Start : from;
        var end = End < to ? End : to;

        return end > start ? this with { Start = start, End = end } : null;
    }
}
