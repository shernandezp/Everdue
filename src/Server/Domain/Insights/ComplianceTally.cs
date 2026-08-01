namespace Everdue.Server.Domain.Insights;

/// <summary>
/// The ledger's outcome rule turned into counters, in one place.
///
/// A period is *concluded* when its <c>PeriodEnd</c> has passed. Only concluded periods enter the
/// denominator, and an occurrence that is still outstanding when its period ends counts as a miss
/// **immediately** — not when the engine's next tick happens to flip the row. The occurrence engine
/// runs on a timer, so reading the stored status alone would make every rate depend on when a
/// background service last ran, which is exactly what the rule "lateness is derived from the
/// period, not the status" exists to prevent.
/// </summary>
public sealed class ComplianceTally
{
    public int OnTime { get; private set; }

    public int Late { get; private set; }

    public int Missed { get; private set; }

    /// <summary>Occurrences whose period is still running: excluded from the rate, reported separately.</summary>
    public int InFlight { get; private set; }

    public int Concluded => OnTime + Late + Missed;

    public int Total => Concluded + InFlight;

    public void Add(WorkItemStatus status, bool periodConcluded)
    {
        switch (status)
        {
            // CompletedLate and Missed are conclusions in themselves: both are only reachable once
            // the period has ended, so neither needs the flag to agree.
            case WorkItemStatus.CompletedLate:
                Late++;
                return;

            case WorkItemStatus.Missed:
                Missed++;
                return;

            // Never produced for an occurrence (cancelling is one-off only), counted nowhere if it is.
            case WorkItemStatus.Cancelled:
                return;
        }

        if (!periodConcluded)
        {
            // Includes work finished early: done, but its period has not been judged yet.
            InFlight++;
            return;
        }

        if (status == WorkItemStatus.Completed)
        {
            OnTime++;
            return;
        }

        // Open, InProgress or OnHold with the period already over. The tick has not caught up; the
        // number does not wait for it.
        Missed++;
    }

    /// <summary>
    /// The published rate, or null when there is nothing to divide — or too little to divide honestly.
    /// A rate over three occurrences is noise, and 95% of 200 is not 100% of 3, so below the
    /// threshold the screens show the raw pair instead.
    /// </summary>
    public double? Rate(int minimumOccurrences)
        => Concluded == 0 || Concluded < minimumOccurrences ? null : (double)OnTime / Concluded;

    /// <summary>True when a rate exists but is being withheld as unreliable — the reason for a "—".</summary>
    public bool IsSuppressed(int minimumOccurrences) => Concluded > 0 && Concluded < minimumOccurrences;
}
