using Ats.Modules.Interviews.Domain;
using Ats.Modules.Notifications.Infrastructure;

namespace Ats.UnitTests.Notifications;

// The cancellation email's closing sentence is the only part of the pipeline that answers the
// question a candidate actually has — "is another invitation coming?" — so it gets its own tests
// rather than riding along on a consumer integration test.
public class InterviewCancelledEmailContentTests
{
    [Fact]
    public void Rescheduling_should_promise_a_new_time()
    {
        var closing = InterviewCancelledEmailConsumer.ClosingFor(
            nameof(InterviewCancellationReason.Rescheduling));

        Assert.Contains("new time", closing, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Position_closed_should_not_promise_a_new_time()
    {
        // The failure that matters: telling someone whose role was cancelled to expect an
        // invitation leaves them waiting on something that will never arrive.
        var closing = InterviewCancelledEmailConsumer.ClosingFor(
            nameof(InterviewCancellationReason.PositionClosed));

        Assert.Contains("no longer open", closing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("we will be in touch", closing, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(InterviewCancellationReason.Rescheduling)]
    [InlineData(InterviewCancellationReason.CandidateRequested)]
    [InlineData(InterviewCancellationReason.CandidateWithdrew)]
    [InlineData(InterviewCancellationReason.PositionClosed)]
    [InlineData(InterviewCancellationReason.Other)]
    public void Every_reason_should_produce_a_distinct_non_empty_closing(
        InterviewCancellationReason reason)
    {
        var closing = InterviewCancelledEmailConsumer.ClosingFor(reason.ToString());

        Assert.False(string.IsNullOrWhiteSpace(closing));
    }

    [Fact]
    public void An_unknown_reason_should_fall_back_rather_than_throw()
    {
        // Reason crosses the module boundary as a string, so an older consumer can meet a newer
        // producer. A vague email beats a poisoned message and no email at all.
        var closing = InterviewCancelledEmailConsumer.ClosingFor("SomethingAddedLater");

        Assert.False(string.IsNullOrWhiteSpace(closing));
    }

    [Fact]
    public void The_reasons_that_close_the_door_should_read_differently_from_the_ones_that_do_not()
    {
        var rescheduling = InterviewCancelledEmailConsumer.ClosingFor(
            nameof(InterviewCancellationReason.Rescheduling));
        var positionClosed = InterviewCancelledEmailConsumer.ClosingFor(
            nameof(InterviewCancellationReason.PositionClosed));

        Assert.NotEqual(rescheduling, positionClosed);
    }
}
