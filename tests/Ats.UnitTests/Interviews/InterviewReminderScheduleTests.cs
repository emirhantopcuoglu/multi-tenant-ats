using Ats.Modules.Interviews.Domain;

namespace Ats.UnitTests.Interviews;

// The reminder schedule is pure arithmetic over (scheduled time, now), which is exactly the kind of
// rule that is cheap to pin here and expensive to discover in an inbox. The sweep that reads these
// columns is covered separately by InterviewReminderJobTests; this file is only about when the
// interview says a reminder is owed.
public class InterviewReminderScheduleTests
{
    private static readonly Guid[] OneInterviewer = [Guid.NewGuid()];
    private static readonly DateTime Now = new(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);

    private const int Duration = 30;

    private static Interview ScheduleAt(DateTime scheduledAtUtc, DateTime? nowUtc = null) =>
        Interview.Schedule(
            applicationId: Guid.NewGuid(), type: InterviewType.Technical,
            scheduledAtUtc: scheduledAtUtc, durationMinutes: Duration,
            interviewerUserIds: OneInterviewer, nowUtc: nowUtc ?? Now, notes: null);

    [Fact]
    public void Scheduling_well_ahead_should_owe_both_reminders()
    {
        var scheduledAt = Now.AddDays(7);

        var interview = ScheduleAt(scheduledAt);

        Assert.Equal(scheduledAt.AddHours(-Interview.DayBeforeReminderLeadHours),
            interview.DayBeforeReminderDueAtUtc);
        // The second reminder is tied to the room opening rather than to a lead time of its own, so
        // the email's join link works the moment it lands.
        Assert.Equal(interview.RoomOpensAtUtc, interview.StartingSoonReminderDueAtUtc);
    }

    [Fact]
    public void Scheduling_inside_a_day_should_owe_no_day_before_reminder()
    {
        // The invitation email this booking produces carries the same facts and arrives now, so a
        // "your interview is tomorrow" reminder would be the same message twice in a row.
        var interview = ScheduleAt(Now.AddHours(2));

        Assert.Null(interview.DayBeforeReminderDueAtUtc);
        Assert.NotNull(interview.StartingSoonReminderDueAtUtc);
    }

    [Fact]
    public void Rescheduling_should_recompute_from_the_new_time()
    {
        // A reminder already owed for the old slot is not credit against the new one: the candidate
        // would be nudged about a meeting that no longer exists at that time.
        var interview = ScheduleAt(Now.AddDays(7));
        var moved = Now.AddDays(3);

        interview.Reschedule(moved, Duration, Now);

        Assert.Equal(moved.AddHours(-Interview.DayBeforeReminderLeadHours),
            interview.DayBeforeReminderDueAtUtc);
        Assert.Equal(moved.AddMinutes(-Interview.RoomOpenLeadMinutes),
            interview.StartingSoonReminderDueAtUtc);
    }

    [Fact]
    public void Rescheduling_into_the_next_day_should_drop_the_day_before_reminder()
    {
        var interview = ScheduleAt(Now.AddDays(7));

        interview.Reschedule(Now.AddHours(3), Duration, Now);

        Assert.Null(interview.DayBeforeReminderDueAtUtc);
    }

    [Theory]
    [InlineData(InterviewStatus.Cancelled)]
    [InlineData(InterviewStatus.Completed)]
    [InlineData(InterviewStatus.NoShow)]
    public void Reaching_a_terminal_status_should_owe_nothing(InterviewStatus terminal)
    {
        // Nothing left to remind anyone about, and an uncleared due instant would keep the row in
        // the sweep's index forever.
        var interview = ScheduleAt(Now.AddDays(7));

        ApplyTerminal(interview, terminal);

        Assert.Null(interview.DayBeforeReminderDueAtUtc);
        Assert.Null(interview.StartingSoonReminderDueAtUtc);
    }

    [Fact]
    public void Clearing_one_reminder_should_leave_the_other_owed()
    {
        var interview = ScheduleAt(Now.AddDays(7));

        interview.ClearReminder(InterviewReminderKind.DayBefore);

        Assert.Null(interview.DayBeforeReminderDueAtUtc);
        Assert.NotNull(interview.StartingSoonReminderDueAtUtc);
    }

    [Fact]
    public void A_started_interview_should_no_longer_be_worth_reminding()
    {
        // What stops a sweep that ran late — after a restart, say — from telling a candidate to join
        // an interview that already began.
        var interview = ScheduleAt(Now.AddDays(1));

        Assert.True(interview.CanRemind(interview.ScheduledAtUtc.AddMinutes(-1)));
        Assert.False(interview.CanRemind(interview.ScheduledAtUtc));
    }

    private static void ApplyTerminal(Interview interview, InterviewStatus terminal)
    {
        // Complete and no-show are only reachable once the slot has started, cancel only before it.
        var afterStart = interview.ScheduledAtUtc.AddMinutes(1);

        switch (terminal)
        {
            case InterviewStatus.Cancelled:
                interview.Cancel(InterviewCancellationReason.PositionClosed, note: null, Now);
                break;
            case InterviewStatus.Completed:
                interview.Complete(afterStart);
                break;
            case InterviewStatus.NoShow:
                interview.MarkNoShow(NoShowParty.Candidate, afterStart);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(terminal), terminal, "Not a terminal status.");
        }
    }
}
