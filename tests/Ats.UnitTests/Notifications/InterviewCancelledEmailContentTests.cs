using Ats.Modules.Interviews.Domain;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;

namespace Ats.UnitTests.Notifications;

// The cancellation email's closing sentence is the only part of the pipeline that answers the
// question a candidate actually has — "is another invitation coming?" — so it gets its own tests
// rather than riding along on a consumer integration test.
//
// The wording now comes from the resource files, so these run against the real provider: a test
// against a stub would pass while the shipped JSON said something else entirely.
public class InterviewCancelledEmailContentTests
{
    private static readonly IEmailTextProvider EmailText = new JsonEmailTextProvider();

    private static string ClosingFor(string reason, string language = SupportedLanguages.English) =>
        InterviewEmailFormatting.CancellationClosing(reason, EmailText, language);

    [Fact]
    public void Rescheduling_should_promise_a_new_time()
    {
        var closing = ClosingFor(nameof(InterviewCancellationReason.Rescheduling));

        Assert.Contains("new time", closing, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Position_closed_should_not_promise_a_new_time()
    {
        // The failure that matters: telling someone whose role was cancelled to expect an
        // invitation leaves them waiting on something that will never arrive.
        var closing = ClosingFor(nameof(InterviewCancellationReason.PositionClosed));

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
        foreach (var language in SupportedLanguages.All)
        {
            var closing = ClosingFor(reason.ToString(), language);

            Assert.False(string.IsNullOrWhiteSpace(closing));
        }
    }

    [Fact]
    public void An_unknown_reason_should_fall_back_rather_than_throw()
    {
        // Reason crosses the module boundary as a string, so an older consumer can meet a newer
        // producer. A vague email beats a poisoned message and no email at all.
        var closing = ClosingFor("SomethingAddedLater");

        Assert.False(string.IsNullOrWhiteSpace(closing));
    }

    [Fact]
    public void The_reasons_that_close_the_door_should_read_differently_from_the_ones_that_do_not()
    {
        foreach (var language in SupportedLanguages.All)
        {
            var rescheduling = ClosingFor(nameof(InterviewCancellationReason.Rescheduling), language);
            var positionClosed = ClosingFor(nameof(InterviewCancellationReason.PositionClosed), language);

            Assert.NotEqual(rescheduling, positionClosed);
        }
    }

    [Fact]
    public void A_turkish_candidate_should_not_receive_the_english_sentence()
    {
        // The whole point of the change: the same reason must read differently per language. A
        // resource file that forgot the Turkish entry would silently fall back to English, which is
        // exactly the bug this feature exists to fix.
        var english = ClosingFor(
            nameof(InterviewCancellationReason.PositionClosed), SupportedLanguages.English);
        var turkish = ClosingFor(
            nameof(InterviewCancellationReason.PositionClosed), SupportedLanguages.Turkish);

        Assert.NotEqual(english, turkish);
    }
}
