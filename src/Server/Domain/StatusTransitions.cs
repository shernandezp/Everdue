namespace Everdue.Server.Domain;

/// <summary>
/// The transition matrix, server-enforced and stated exactly once. Anything not listed here is
/// rejected; every allowed transition writes a <see cref="WorkItemEvent"/> at the call site.
/// </summary>
public static class StatusTransitions
{
    private readonly record struct Transition(
        WorkItemStatus From,
        WorkItemStatus To,
        TransitionActor Actor,
        Scope Scope);

    private enum Scope
    {
        Any = 0,
        OccurrenceOnly = 1,
        OneOffOnly = 2,
    }

    private static readonly Transition[] Allowed =
    [
        // Picking work up and putting it back down.
        new(WorkItemStatus.Open,          WorkItemStatus.InProgress,    TransitionActor.User,   Scope.Any),
        new(WorkItemStatus.InProgress,    WorkItemStatus.Open,          TransitionActor.User,   Scope.Any),
        new(WorkItemStatus.OnHold,        WorkItemStatus.InProgress,    TransitionActor.User,   Scope.Any),
        new(WorkItemStatus.InProgress,    WorkItemStatus.OnHold,        TransitionActor.User,   Scope.Any),

        // Finishing. Completing straight from Open stays legal — nobody should have to click twice.
        new(WorkItemStatus.Open,          WorkItemStatus.Completed,     TransitionActor.User,   Scope.Any),
        new(WorkItemStatus.InProgress,    WorkItemStatus.Completed,     TransitionActor.User,   Scope.Any),
        new(WorkItemStatus.OnHold,        WorkItemStatus.Completed,     TransitionActor.User,   Scope.Any),

        // Finishing an occurrence whose period has already ended, before the engine's next tick has
        // flipped it. Late is decided by the period, never by which minute the tick happened to run.
        new(WorkItemStatus.Open,          WorkItemStatus.CompletedLate, TransitionActor.User,   Scope.OccurrenceOnly),
        new(WorkItemStatus.InProgress,    WorkItemStatus.CompletedLate, TransitionActor.User,   Scope.OccurrenceOnly),
        new(WorkItemStatus.OnHold,        WorkItemStatus.CompletedLate, TransitionActor.User,   Scope.OccurrenceOnly),
        new(WorkItemStatus.Missed,        WorkItemStatus.CompletedLate, TransitionActor.User,   Scope.Any),

        new(WorkItemStatus.Open,          WorkItemStatus.OnHold,        TransitionActor.User,   Scope.Any),
        new(WorkItemStatus.OnHold,        WorkItemStatus.Open,          TransitionActor.User,   Scope.Any),

        // Only the engine records a miss, and it can catch any state that was still outstanding.
        new(WorkItemStatus.Open,          WorkItemStatus.Missed,        TransitionActor.Engine, Scope.OccurrenceOnly),
        new(WorkItemStatus.InProgress,    WorkItemStatus.Missed,        TransitionActor.Engine, Scope.OccurrenceOnly),
        new(WorkItemStatus.OnHold,        WorkItemStatus.Missed,        TransitionActor.Engine, Scope.OccurrenceOnly),

        // Undo.
        new(WorkItemStatus.Completed,     WorkItemStatus.Open,          TransitionActor.User,   Scope.Any),
        new(WorkItemStatus.CompletedLate, WorkItemStatus.Open,          TransitionActor.User,   Scope.Any),

        new(WorkItemStatus.Open,          WorkItemStatus.Cancelled,     TransitionActor.User,   Scope.OneOffOnly),
        new(WorkItemStatus.InProgress,    WorkItemStatus.Cancelled,     TransitionActor.User,   Scope.OneOffOnly),
        new(WorkItemStatus.OnHold,        WorkItemStatus.Cancelled,     TransitionActor.User,   Scope.OneOffOnly),

        // Deliberately absent: Missed -> InProgress. Compliance counts Missed and CompletedLate, so
        // letting a missed item move back into an in-flight state would drop it out of the 30/60/90
        // day counts while someone worked on it. A miss stays visible until it is completed late.
    ];

    public static bool IsAllowed(WorkItemStatus from, WorkItemStatus to, TransitionActor actor, bool isOccurrence)
        => Allowed.Any(t => t.From == from
                            && t.To == to
                            && t.Actor == actor
                            && MatchesScope(t.Scope, isOccurrence));

    /// <summary>
    /// The status a "complete" action produces.
    ///
    /// Lateness is a property of the <em>period</em>, not of the row's current status. The engine
    /// ticks every few minutes, so an occurrence finished just after its period ended is still
    /// marked Open when the user clicks — reading the status alone would record that as an on-time
    /// completion and quietly erase a miss on a timer boundary.
    /// </summary>
    public static WorkItemStatus CompletionTargetFor(WorkItemStatus from, DateTimeOffset? periodEnd, DateTimeOffset now)
    {
        if (from == WorkItemStatus.Missed)
        {
            return WorkItemStatus.CompletedLate;
        }

        return periodEnd is { } end && end <= now
            ? WorkItemStatus.CompletedLate
            : WorkItemStatus.Completed;
    }

    /// <summary>All transitions a user may perform from <paramref name="from"/> — used to drive the board's affordances.</summary>
    public static IReadOnlyList<WorkItemStatus> UserTransitionsFrom(WorkItemStatus from, bool isOccurrence)
        => Allowed
            .Where(t => t.From == from && t.Actor == TransitionActor.User && MatchesScope(t.Scope, isOccurrence))
            .Select(t => t.To)
            .Distinct()
            .ToArray();

    private static bool MatchesScope(Scope scope, bool isOccurrence) => scope switch
    {
        Scope.Any => true,
        Scope.OccurrenceOnly => isOccurrence,
        Scope.OneOffOnly => !isOccurrence,
        _ => false,
    };
}
