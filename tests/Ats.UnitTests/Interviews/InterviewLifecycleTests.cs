using Ats.Modules.Interviews.Domain;

namespace Ats.UnitTests.Interviews;

public class InterviewLifecycleTests
{
    private static readonly Guid[] OneInterviewer = [Guid.NewGuid()];

    private static Interview ScheduleValid() =>
        Interview.Schedule(
            applicationId: Guid.NewGuid(), type: InterviewType.Technical,
            scheduledAtUtc: DateTime.UtcNow.AddDays(1), durationMinutes: 60,
            location: "Zoom", interviewerUserIds: OneInterviewer, notes: null);

    [Fact]
    public void Schedule_should_start_in_scheduled_status()
    {
        var interview = ScheduleValid();

        Assert.Equal(InterviewStatus.Scheduled, interview.Status);
        Assert.Single(interview.InterviewerUserIds);
        Assert.Equal(60, interview.DurationMinutes);
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
    public void Reschedule_should_keep_the_same_room_token()
    {
        var interview = ScheduleValid();
        var originalToken = interview.RoomToken;

        interview.Reschedule(DateTime.UtcNow.AddDays(2), 45);

        Assert.Equal(originalToken, interview.RoomToken);
    }

    [Fact]
    public void IsRoomOpen_should_be_false_before_the_lead_window()
    {
        var interview = ScheduleValid();

        var justBeforeOpen = interview.RoomOpensAtUtc.AddSeconds(-1);

        Assert.False(interview.IsRoomOpen(justBeforeOpen));
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
    public void IsRoomOpen_should_be_false_once_the_interview_is_no_longer_scheduled()
    {
        var interview = ScheduleValid();
        interview.Cancel();

        Assert.False(interview.IsRoomOpen(interview.ScheduledAtUtc));
    }

    [Fact]
    public void Schedule_should_deduplicate_interviewers()
    {
        var interviewer = Guid.NewGuid();

        var interview = Interview.Schedule(
            Guid.NewGuid(), InterviewType.Final, DateTime.UtcNow.AddDays(1), 30,
            null, [interviewer, interviewer], null);

        Assert.Single(interview.InterviewerUserIds);
    }

    [Fact]
    public void Schedule_should_throw_when_no_interviewers()
    {
        var act = () => Interview.Schedule(
            Guid.NewGuid(), InterviewType.PhoneScreen, DateTime.UtcNow.AddDays(1), 30, null, [], null);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Schedule_should_throw_when_in_the_past()
    {
        var act = () => Interview.Schedule(
            Guid.NewGuid(), InterviewType.PhoneScreen, DateTime.UtcNow.AddMinutes(-1), 30,
            null, OneInterviewer, null);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Schedule_should_throw_when_duration_not_positive()
    {
        var act = () => Interview.Schedule(
            Guid.NewGuid(), InterviewType.PhoneScreen, DateTime.UtcNow.AddDays(1), 0,
            null, OneInterviewer, null);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Reschedule_should_update_time_and_duration_while_scheduled()
    {
        var interview = ScheduleValid();
        var newTime = DateTime.UtcNow.AddDays(2);

        interview.Reschedule(newTime, 45);

        Assert.Equal(newTime, interview.ScheduledAtUtc);
        Assert.Equal(45, interview.DurationMinutes);
        Assert.Equal(InterviewStatus.Scheduled, interview.Status);
    }

    [Fact]
    public void Cancel_should_set_status_to_cancelled()
    {
        var interview = ScheduleValid();

        interview.Cancel();

        Assert.Equal(InterviewStatus.Cancelled, interview.Status);
    }

    [Fact]
    public void Complete_should_set_status_to_completed()
    {
        var interview = ScheduleValid();

        interview.Complete();

        Assert.Equal(InterviewStatus.Completed, interview.Status);
    }

    [Fact]
    public void MarkNoShow_should_set_status_to_no_show()
    {
        var interview = ScheduleValid();

        interview.MarkNoShow();

        Assert.Equal(InterviewStatus.NoShow, interview.Status);
    }

    [Fact]
    public void A_terminal_interview_cannot_transition_again()
    {
        var interview = ScheduleValid();
        interview.Complete();

        Assert.Throws<InvalidOperationException>(() => interview.Cancel());
        Assert.Throws<InvalidOperationException>(() => interview.Reschedule(DateTime.UtcNow.AddDays(1), 30));
        Assert.Throws<InvalidOperationException>(() => interview.MarkNoShow());
    }
}
