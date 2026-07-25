using Ats.Modules.Interviews.Domain;

namespace Ats.UnitTests.Interviews;

public class InterviewLifecycleTests
{
    private static readonly Guid[] OneInterviewer = [Guid.NewGuid()];

    // A fixed clock rather than DateTime.UtcNow. Every rule under test is a statement about where
    // "now" sits relative to the interview, so pinning both ends is what makes assertions like
    // "cancelling after the start time is refused" expressible without actually waiting.
    private static readonly DateTime Now = new(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);

    private const int Duration = 60;

    private static Interview ScheduleValid() =>
        Interview.Schedule(
            applicationId: Guid.NewGuid(), type: InterviewType.Technical,
            scheduledAtUtc: Now.AddDays(1), durationMinutes: Duration,
            interviewerUserIds: OneInterviewer, nowUtc: Now, notes: null);

    /* Anchors relative to an interview scheduled for Now + 1 day. */
    private static DateTime BeforeStart(Interview i) => i.ScheduledAtUtc.AddMinutes(-1);
    private static DateTime AfterStart(Interview i) => i.ScheduledAtUtc.AddMinutes(1);
    private static DateTime AfterEnd(Interview i) => i.EndsAtUtc.AddMinutes(1);

    // ---- Creation ----

    [Fact]
    public void Schedule_should_start_in_scheduled_status()
    {
        var interview = ScheduleValid();

        Assert.Equal(InterviewStatus.Scheduled, interview.Status);
        Assert.Single(interview.InterviewerUserIds);
        Assert.Equal(Duration, interview.DurationMinutes);
    }

    [Fact]
    public void Schedule_should_generate_a_unique_room_token()
    {
        var first = ScheduleValid();
        var second = ScheduleValid();

        Assert.False(string.IsNullOrWhiteSpace(first.RoomToken));
        Assert.NotEqual(first.RoomToken, second.RoomToken);
    }

    [Fact]
    public void Schedule_should_not_create_a_room_for_a_phone_screen()
    {
        var interview = Interview.Schedule(
            Guid.NewGuid(), InterviewType.PhoneScreen, Now.AddDays(1), 30, OneInterviewer, Now);

        Assert.Null(interview.RoomToken);
        // A phone screen never has an open room, even inside the time window.
        Assert.False(interview.IsRoomOpen(interview.ScheduledAtUtc));
    }

    [Fact]
    public void Schedule_should_deduplicate_interviewers()
    {
        var interviewer = Guid.NewGuid();

        var interview = Interview.Schedule(
            Guid.NewGuid(), InterviewType.Final, Now.AddDays(1), 30,
            [interviewer, interviewer], Now);

        Assert.Single(interview.InterviewerUserIds);
    }

    [Fact]
    public void Schedule_should_throw_when_no_interviewers()
    {
        var act = () => Interview.Schedule(
            Guid.NewGuid(), InterviewType.PhoneScreen, Now.AddDays(1), 30, [], Now);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Schedule_should_throw_when_in_the_past()
    {
        var act = () => Interview.Schedule(
            Guid.NewGuid(), InterviewType.PhoneScreen, Now.AddMinutes(-1), 30, OneInterviewer, Now);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Schedule_should_throw_when_inside_the_minimum_lead_time()
    {
        // Nominally in the future, but too soon to be real: the invitation would arrive after the
        // candidate was meant to join.
        var tooSoon = Now.AddMinutes(Interview.MinimumLeadMinutes - 1);

        var act = () => Interview.Schedule(
            Guid.NewGuid(), InterviewType.Technical, tooSoon, 30, OneInterviewer, Now);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Schedule_should_accept_a_slot_exactly_at_the_minimum_lead_time()
    {
        var earliest = Now.AddMinutes(Interview.MinimumLeadMinutes);

        var interview = Interview.Schedule(
            Guid.NewGuid(), InterviewType.Technical, earliest, 30, OneInterviewer, Now);

        Assert.Equal(earliest, interview.ScheduledAtUtc);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(90)]
    [InlineData(6000)]
    public void Schedule_should_throw_when_duration_is_not_a_preset(int minutes)
    {
        var act = () => Interview.Schedule(
            Guid.NewGuid(), InterviewType.Technical, Now.AddDays(1), minutes, OneInterviewer, Now);

        Assert.Throws<ArgumentException>(act);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    public void Schedule_should_accept_each_preset_duration(int minutes)
    {
        var interview = Interview.Schedule(
            Guid.NewGuid(), InterviewType.Technical, Now.AddDays(1), minutes, OneInterviewer, Now);

        Assert.Equal(minutes, interview.DurationMinutes);
    }

    // ---- Derived state: the gap between "Scheduled" and "actually still pending" ----

    [Fact]
    public void IsAwaitingOutcome_should_be_false_while_the_interview_is_still_ahead()
    {
        var interview = ScheduleValid();

        Assert.False(interview.IsAwaitingOutcome(Now));
        Assert.False(interview.IsAwaitingOutcome(BeforeStart(interview)));
    }

    [Fact]
    public void IsAwaitingOutcome_should_be_false_while_the_interview_is_underway()
    {
        var interview = ScheduleValid();

        Assert.False(interview.IsAwaitingOutcome(AfterStart(interview)));
    }

    [Fact]
    public void IsAwaitingOutcome_should_be_true_once_the_slot_has_passed_unresolved()
    {
        // The reported bug: the row still says Scheduled, but the appointment is over and nobody
        // recorded what happened.
        var interview = ScheduleValid();

        Assert.True(interview.IsAwaitingOutcome(AfterEnd(interview)));
    }

    [Fact]
    public void IsAwaitingOutcome_should_be_false_once_an_outcome_was_recorded()
    {
        var interview = ScheduleValid();
        interview.Complete(AfterStart(interview));

        Assert.False(interview.IsAwaitingOutcome(AfterEnd(interview)));
    }

    [Fact]
    public void EndsAtUtc_should_be_the_start_plus_the_duration()
    {
        var interview = ScheduleValid();

        Assert.Equal(interview.ScheduledAtUtc.AddMinutes(Duration), interview.EndsAtUtc);
    }

    // ---- Transitions before the start time: reschedule and cancel ----

    [Fact]
    public void Reschedule_should_update_time_and_duration_before_the_start()
    {
        var interview = ScheduleValid();
        var newTime = Now.AddDays(2);

        interview.Reschedule(newTime, 45, Now);

        Assert.Equal(newTime, interview.ScheduledAtUtc);
        Assert.Equal(45, interview.DurationMinutes);
        Assert.Equal(InterviewStatus.Scheduled, interview.Status);
    }

    [Fact]
    public void Reschedule_should_keep_the_same_room_token()
    {
        var interview = ScheduleValid();
        var originalToken = interview.RoomToken;

        interview.Reschedule(Now.AddDays(2), 45, Now);

        Assert.Equal(originalToken, interview.RoomToken);
    }

    [Fact]
    public void Reschedule_should_throw_when_duration_is_not_a_preset()
    {
        var interview = ScheduleValid();

        Assert.Throws<ArgumentException>(() => interview.Reschedule(Now.AddDays(2), 25, Now));
    }

    [Fact]
    public void Reschedule_should_throw_once_the_start_time_has_passed()
    {
        // Moving an elapsed interview would erase the fact that the original slot was missed —
        // mark it NoShow and book a new one instead.
        var interview = ScheduleValid();

        Assert.Throws<InvalidOperationException>(
            () => interview.Reschedule(Now.AddDays(3), 30, AfterEnd(interview)));
    }

    [Fact]
    public void Cancel_should_set_status_to_cancelled_before_the_start()
    {
        var interview = ScheduleValid();

        interview.Cancel(InterviewCancellationReason.Other, null, BeforeStart(interview));

        Assert.Equal(InterviewStatus.Cancelled, interview.Status);
    }

    [Fact]
    public void Cancel_should_record_the_reason_and_the_internal_note()
    {
        var interview = ScheduleValid();

        interview.Cancel(
            InterviewCancellationReason.PositionClosed, "  budget pulled  ", BeforeStart(interview));

        Assert.Equal(InterviewCancellationReason.PositionClosed, interview.CancellationReason);
        Assert.Equal("budget pulled", interview.CancellationNote);
    }

    [Fact]
    public void Cancel_should_accept_a_reason_without_a_note()
    {
        var interview = ScheduleValid();

        interview.Cancel(InterviewCancellationReason.Rescheduling, null, BeforeStart(interview));

        Assert.Equal(InterviewCancellationReason.Rescheduling, interview.CancellationReason);
        Assert.Null(interview.CancellationNote);
    }

    [Fact]
    public void Cancel_should_treat_a_blank_note_as_no_note()
    {
        var interview = ScheduleValid();

        interview.Cancel(InterviewCancellationReason.Other, "   ", BeforeStart(interview));

        Assert.Null(interview.CancellationNote);
    }

    [Fact]
    public void Cancel_should_throw_on_an_undefined_reason()
    {
        var interview = ScheduleValid();

        Assert.Throws<ArgumentException>(
            () => interview.Cancel((InterviewCancellationReason)99, null, BeforeStart(interview)));
    }

    [Fact]
    public void A_refused_cancel_should_not_record_a_reason()
    {
        // The guard has to run before any state is written, or a rejected cancellation would still
        // leave its reason behind on a live interview.
        var interview = ScheduleValid();

        Assert.Throws<InvalidOperationException>(
            () => interview.Cancel(InterviewCancellationReason.Other, "note", AfterEnd(interview)));

        Assert.Null(interview.CancellationReason);
        Assert.Null(interview.CancellationNote);
    }

    [Fact]
    public void An_interview_that_was_never_cancelled_should_carry_no_reason()
    {
        var interview = ScheduleValid();
        interview.Complete(AfterStart(interview));

        Assert.Null(interview.CancellationReason);
        Assert.Null(interview.CancellationNote);
    }

    [Fact]
    public void Cancel_should_throw_once_the_start_time_has_passed()
    {
        // The bug from the screenshot: "cancel" was offered for an appointment that was already
        // over. Cancelling means "this will not happen", which is no longer a truthful claim.
        var interview = ScheduleValid();

        Assert.Throws<InvalidOperationException>(() => interview.Cancel(InterviewCancellationReason.Other, null, AfterEnd(interview)));
        Assert.Equal(InterviewStatus.Scheduled, interview.Status);
    }

    // ---- Transitions after the start time: complete and no-show ----

    [Fact]
    public void Complete_should_set_status_to_completed_once_underway()
    {
        var interview = ScheduleValid();

        interview.Complete(AfterStart(interview));

        Assert.Equal(InterviewStatus.Completed, interview.Status);
    }

    [Fact]
    public void Complete_should_throw_before_the_interview_has_started()
    {
        // The mirror of the reported bug: tomorrow's calendar cannot be cleared by marking it done
        // today.
        var interview = ScheduleValid();

        Assert.Throws<InvalidOperationException>(() => interview.Complete(Now));
        Assert.Equal(InterviewStatus.Scheduled, interview.Status);
    }

    [Fact]
    public void MarkNoShow_should_set_status_to_no_show_once_underway()
    {
        var interview = ScheduleValid();

        interview.MarkNoShow(AfterStart(interview));

        Assert.Equal(InterviewStatus.NoShow, interview.Status);
    }

    [Fact]
    public void MarkNoShow_should_throw_before_the_interview_has_started()
    {
        var interview = ScheduleValid();

        Assert.Throws<InvalidOperationException>(() => interview.MarkNoShow(Now));
    }

    [Fact]
    public void A_terminal_interview_cannot_transition_again()
    {
        var interview = ScheduleValid();
        var afterStart = AfterStart(interview);
        interview.Complete(afterStart);

        Assert.Throws<InvalidOperationException>(() => interview.Cancel(InterviewCancellationReason.Other, null, afterStart));
        Assert.Throws<InvalidOperationException>(() => interview.MarkNoShow(afterStart));
        Assert.Throws<InvalidOperationException>(
            () => interview.Reschedule(Now.AddDays(5), 30, afterStart));
    }

    // ---- Capability flags: exactly one pair is offered at any moment ----

    [Fact]
    public void Only_reschedule_and_cancel_should_be_offered_before_the_start()
    {
        var interview = ScheduleValid();
        var before = BeforeStart(interview);

        Assert.True(interview.CanReschedule(before));
        Assert.True(interview.CanCancel(before));
        Assert.False(interview.CanComplete(before));
        Assert.False(interview.CanMarkNoShow(before));
    }

    [Fact]
    public void Only_complete_and_no_show_should_be_offered_after_the_start()
    {
        var interview = ScheduleValid();
        var after = AfterEnd(interview);

        Assert.False(interview.CanReschedule(after));
        Assert.False(interview.CanCancel(after));
        Assert.True(interview.CanComplete(after));
        Assert.True(interview.CanMarkNoShow(after));
    }

    [Fact]
    public void No_action_should_be_offered_once_the_interview_is_terminal()
    {
        var interview = ScheduleValid();
        var after = AfterStart(interview);
        interview.Complete(after);

        Assert.False(interview.CanReschedule(after));
        Assert.False(interview.CanCancel(after));
        Assert.False(interview.CanComplete(after));
        Assert.False(interview.CanMarkNoShow(after));
    }

    // ---- Feedback eligibility ----

    [Fact]
    public void CanReceiveFeedback_should_be_false_before_the_interview_has_ended()
    {
        var interview = ScheduleValid();

        Assert.False(interview.CanReceiveFeedback(Now));
        Assert.False(interview.CanReceiveFeedback(AfterStart(interview)));
    }

    [Fact]
    public void CanReceiveFeedback_should_be_true_once_the_scheduled_end_has_passed()
    {
        var interview = ScheduleValid();

        Assert.True(interview.CanReceiveFeedback(AfterEnd(interview)));
    }

    [Fact]
    public void CanReceiveFeedback_should_be_true_for_a_completed_interview_regardless_of_time()
    {
        var interview = ScheduleValid();
        interview.Complete(AfterStart(interview));

        Assert.True(interview.CanReceiveFeedback(AfterStart(interview)));
    }

    [Fact]
    public void CanReceiveFeedback_should_be_false_for_a_cancelled_interview()
    {
        var interview = ScheduleValid();
        interview.Cancel(InterviewCancellationReason.Other, null, BeforeStart(interview));

        Assert.False(interview.CanReceiveFeedback(AfterEnd(interview)));
    }

    [Fact]
    public void CanReceiveFeedback_should_be_false_for_a_no_show()
    {
        var interview = ScheduleValid();
        interview.MarkNoShow(AfterStart(interview));

        Assert.False(interview.CanReceiveFeedback(AfterEnd(interview)));
    }

    // ---- Room window ----
    // The room stays keyed to the clock, not to the outcome, so an interview that has elapsed
    // without being resolved still closes its room on schedule.

    [Fact]
    public void IsRoomOpen_should_be_false_before_the_lead_window()
    {
        var interview = ScheduleValid();

        Assert.False(interview.IsRoomOpen(interview.RoomOpensAtUtc.AddSeconds(-1)));
    }

    [Fact]
    public void IsRoomOpen_should_be_true_inside_the_lead_window_and_during_the_interview()
    {
        var interview = ScheduleValid();

        Assert.True(interview.IsRoomOpen(interview.RoomOpensAtUtc));
        Assert.True(interview.IsRoomOpen(interview.ScheduledAtUtc));
    }

    [Fact]
    public void IsRoomOpen_should_be_true_within_the_grace_period_and_false_after()
    {
        var interview = ScheduleValid();

        Assert.True(interview.IsRoomOpen(interview.RoomClosesAtUtc));
        Assert.False(interview.IsRoomOpen(interview.RoomClosesAtUtc.AddSeconds(1)));
    }

    [Fact]
    public void IsRoomOpen_should_stay_open_during_the_grace_period_of_an_unresolved_interview()
    {
        // An interview can be awaiting an outcome and still have a reachable room: the grace period
        // outlives the scheduled end, which is exactly when a running-over call needs it.
        var interview = ScheduleValid();
        var justAfterEnd = interview.EndsAtUtc.AddMinutes(1);

        Assert.True(interview.IsAwaitingOutcome(justAfterEnd));
        Assert.True(interview.IsRoomOpen(justAfterEnd));
    }

    [Fact]
    public void IsRoomOpen_should_be_false_once_the_interview_is_no_longer_scheduled()
    {
        var interview = ScheduleValid();
        interview.Cancel(InterviewCancellationReason.Other, null, BeforeStart(interview));

        Assert.False(interview.IsRoomOpen(interview.ScheduledAtUtc));
    }
}
