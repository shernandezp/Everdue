using Everdue.Server.Domain;

namespace Everdue.Server.Tests.Domain;

/// <summary>
/// The allowed status transitions, asserted directly. The API tests then prove the same matrix is
/// what the endpoints actually enforce.
/// </summary>
public class StatusTransitionTests
{
    public static TheoryData<WorkItemStatus, WorkItemStatus, TransitionActor, bool> AllowedTransitions => new()
    {
        { WorkItemStatus.Open, WorkItemStatus.Completed, TransitionActor.User, true },
        { WorkItemStatus.Open, WorkItemStatus.OnHold, TransitionActor.User, true },
        { WorkItemStatus.OnHold, WorkItemStatus.Open, TransitionActor.User, true },
        { WorkItemStatus.OnHold, WorkItemStatus.Completed, TransitionActor.User, true },
        { WorkItemStatus.Open, WorkItemStatus.Missed, TransitionActor.Engine, true },
        { WorkItemStatus.OnHold, WorkItemStatus.Missed, TransitionActor.Engine, true },
        { WorkItemStatus.Missed, WorkItemStatus.CompletedLate, TransitionActor.User, true },
        { WorkItemStatus.Completed, WorkItemStatus.Open, TransitionActor.User, true },
        { WorkItemStatus.Open, WorkItemStatus.Cancelled, TransitionActor.User, false },
        { WorkItemStatus.OnHold, WorkItemStatus.Cancelled, TransitionActor.User, false },
    };

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void Every_documented_transition_is_allowed(WorkItemStatus from, WorkItemStatus to, TransitionActor actor, bool isOccurrence)
    {
        StatusTransitions.IsAllowed(from, to, actor, isOccurrence).ShouldBeTrue($"{from} -> {to} by {actor}");
    }

    [Fact]
    public void Only_the_engine_may_mark_something_missed()
    {
        StatusTransitions.IsAllowed(WorkItemStatus.Open, WorkItemStatus.Missed, TransitionActor.User, isOccurrence: true).ShouldBeFalse();
        StatusTransitions.IsAllowed(WorkItemStatus.OnHold, WorkItemStatus.Missed, TransitionActor.User, isOccurrence: true).ShouldBeFalse();
    }

    [Fact]
    public void A_one_off_task_can_never_be_missed()
    {
        StatusTransitions.IsAllowed(WorkItemStatus.Open, WorkItemStatus.Missed, TransitionActor.Engine, isOccurrence: false).ShouldBeFalse();
    }

    [Fact]
    public void An_occurrence_can_never_be_cancelled()
    {
        StatusTransitions.IsAllowed(WorkItemStatus.Open, WorkItemStatus.Cancelled, TransitionActor.User, isOccurrence: true).ShouldBeFalse();
        StatusTransitions.IsAllowed(WorkItemStatus.OnHold, WorkItemStatus.Cancelled, TransitionActor.User, isOccurrence: true).ShouldBeFalse();
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-28T12:00:00Z");

    [Fact]
    public void A_missed_item_completes_late_and_can_never_complete_normally()
    {
        StatusTransitions.CompletionTargetFor(WorkItemStatus.Missed, null, Now).ShouldBe(WorkItemStatus.CompletedLate);
        StatusTransitions.IsAllowed(WorkItemStatus.Missed, WorkItemStatus.Completed, TransitionActor.User, isOccurrence: true).ShouldBeFalse();
    }

    /// <summary>
    /// The engine ticks on a timer, so between a period ending and the next tick an occurrence is
    /// still Open. Completing in that window used to record an on-time completion — a miss erased
    /// by a timer boundary. Lateness is decided by the period, never by the status.
    /// </summary>
    [Fact]
    public void An_occurrence_finished_after_its_period_ended_is_late_even_before_the_engine_notices()
    {
        var endedAnHourAgo = Now.AddHours(-1);
        var endsTomorrow = Now.AddDays(1);

        StatusTransitions.CompletionTargetFor(WorkItemStatus.Open, endedAnHourAgo, Now).ShouldBe(WorkItemStatus.CompletedLate);
        StatusTransitions.CompletionTargetFor(WorkItemStatus.InProgress, endedAnHourAgo, Now).ShouldBe(WorkItemStatus.CompletedLate);
        StatusTransitions.CompletionTargetFor(WorkItemStatus.OnHold, endedAnHourAgo, Now).ShouldBe(WorkItemStatus.CompletedLate);

        StatusTransitions.CompletionTargetFor(WorkItemStatus.Open, endsTomorrow, Now).ShouldBe(WorkItemStatus.Completed);

        // A one-off has no period, so it is never late.
        StatusTransitions.CompletionTargetFor(WorkItemStatus.Open, null, Now).ShouldBe(WorkItemStatus.Completed);
    }

    [Fact]
    public void Work_can_be_picked_up_and_put_back_down()
    {
        StatusTransitions.IsAllowed(WorkItemStatus.Open, WorkItemStatus.InProgress, TransitionActor.User, isOccurrence: true).ShouldBeTrue();
        StatusTransitions.IsAllowed(WorkItemStatus.OnHold, WorkItemStatus.InProgress, TransitionActor.User, isOccurrence: true).ShouldBeTrue();
        StatusTransitions.IsAllowed(WorkItemStatus.InProgress, WorkItemStatus.Open, TransitionActor.User, isOccurrence: true).ShouldBeTrue();
        StatusTransitions.IsAllowed(WorkItemStatus.InProgress, WorkItemStatus.OnHold, TransitionActor.User, isOccurrence: true).ShouldBeTrue();
        StatusTransitions.IsAllowed(WorkItemStatus.InProgress, WorkItemStatus.Completed, TransitionActor.User, isOccurrence: true).ShouldBeTrue();

        // Completing straight from the to-do column stays legal: nobody should have to click twice.
        StatusTransitions.IsAllowed(WorkItemStatus.Open, WorkItemStatus.Completed, TransitionActor.User, isOccurrence: true).ShouldBeTrue();
    }

    /// <summary>
    /// Compliance counts Missed and CompletedLate. If a missed item could move back into an
    /// in-flight state it would drop out of the 30/60/90-day counts while someone worked on it.
    /// </summary>
    [Fact]
    public void A_missed_item_can_never_move_back_into_progress()
    {
        StatusTransitions.IsAllowed(WorkItemStatus.Missed, WorkItemStatus.InProgress, TransitionActor.User, isOccurrence: true).ShouldBeFalse();
        StatusTransitions.UserTransitionsFrom(WorkItemStatus.Missed, isOccurrence: true).ShouldBe([WorkItemStatus.CompletedLate]);
    }

    [Fact]
    public void Work_in_progress_is_still_outstanding_and_the_engine_can_still_miss_it()
    {
        WorkItemStatus.InProgress.IsOutstanding().ShouldBeTrue();
        WorkItemStatus.InProgress.IsWorkable().ShouldBeTrue();
        WorkItemStatus.InProgress.IsCompletion().ShouldBeFalse();
        WorkItemStatus.InProgress.CountsAsMissed().ShouldBeFalse();

        StatusTransitions
            .IsAllowed(WorkItemStatus.InProgress, WorkItemStatus.Missed, TransitionActor.Engine, isOccurrence: true)
            .ShouldBeTrue();

        // …but a user still cannot record a miss.
        StatusTransitions
            .IsAllowed(WorkItemStatus.InProgress, WorkItemStatus.Missed, TransitionActor.User, isOccurrence: true)
            .ShouldBeFalse();
    }

    [Fact]
    public void Terminal_and_nonsensical_moves_are_rejected()
    {
        StatusTransitions.IsAllowed(WorkItemStatus.Cancelled, WorkItemStatus.Open, TransitionActor.User, isOccurrence: false).ShouldBeFalse();
        StatusTransitions.IsAllowed(WorkItemStatus.Completed, WorkItemStatus.Missed, TransitionActor.Engine, isOccurrence: true).ShouldBeFalse();
        StatusTransitions.IsAllowed(WorkItemStatus.Missed, WorkItemStatus.OnHold, TransitionActor.User, isOccurrence: true).ShouldBeFalse();
        StatusTransitions.IsAllowed(WorkItemStatus.Open, WorkItemStatus.Open, TransitionActor.User, isOccurrence: true).ShouldBeFalse();
    }

    /// <summary>
    /// The laundering hole this closes: Missed -> complete (CompletedLate) -> reopen (Open) made the
    /// item neither Missed nor CompletedLate, so it vanished from the 30/60/90-day miss counts until
    /// the engine's next tick — or forever, with the engine disabled. A CompletedLate row always sits
    /// on a closed period, so reopening it is never a meaningful action; the transition does not exist.
    /// </summary>
    [Fact]
    public void A_late_completion_can_never_be_reopened()
    {
        StatusTransitions.IsAllowed(WorkItemStatus.CompletedLate, WorkItemStatus.Open, TransitionActor.User, isOccurrence: true).ShouldBeFalse();
        StatusTransitions.IsAllowed(WorkItemStatus.CompletedLate, WorkItemStatus.Open, TransitionActor.User, isOccurrence: false).ShouldBeFalse();
        StatusTransitions.UserTransitionsFrom(WorkItemStatus.CompletedLate, isOccurrence: true).ShouldBeEmpty();
    }

    [Fact]
    public void CompletedLate_counts_as_missed_for_compliance_and_as_completed_for_activity()
    {
        WorkItemStatus.CompletedLate.CountsAsMissed().ShouldBeTrue();
        WorkItemStatus.CompletedLate.IsCompletion().ShouldBeTrue();

        WorkItemStatus.Completed.CountsAsMissed().ShouldBeFalse();
        WorkItemStatus.Missed.IsCompletion().ShouldBeFalse();
    }

    [Fact]
    public void UserTransitionsFrom_offers_cancel_only_for_one_off_tasks()
    {
        StatusTransitions.UserTransitionsFrom(WorkItemStatus.Open, isOccurrence: false).ShouldContain(WorkItemStatus.Cancelled);
        StatusTransitions.UserTransitionsFrom(WorkItemStatus.Open, isOccurrence: true).ShouldNotContain(WorkItemStatus.Cancelled);
    }
}
