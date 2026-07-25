using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;

namespace Ats.IntegrationTests.Interviews;

// The read path for interview feedback, which until now did not exist: evaluations were written and
// nothing ever queried them back.
//
// The withholding rule gets the most coverage because it is the part that can quietly do harm. The
// entity is immutable so a score cannot be softened after the fact; letting an evaluator read the
// panel before filing their own would move that anchoring earlier rather than prevent it.
[Collection("Integration")]
public sealed class GetInterviewFeedbackTests
{
    private readonly PostgresContainerFixture _fixture;

    public GetInterviewFeedbackTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_return_nothing_for_an_interview_with_no_feedback()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var recruiter = Guid.NewGuid();
        var interview = await SeedAsync(tenant, [Guid.NewGuid()]);

        var summary = await QueryAsync(tenant, interview.Id, recruiter);

        Assert.Empty(summary.Items);
        Assert.Equal(0, summary.SubmittedCount);
        Assert.Equal(1, summary.ExpectedCount);
        Assert.Null(summary.AverageRating);
        Assert.False(summary.IsWithheld);
    }

    [Fact]
    public async Task should_return_the_panels_feedback_to_a_non_participant()
    {
        // A recruiter reading the result is a decision-maker, not an evaluator: there is no score of
        // theirs to be anchored, so nothing is withheld.
        var tenant = new FixedTenant(Guid.NewGuid());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var interview = await SeedAsync(tenant, [first, second]);
        await AddFeedbackAsync(tenant, interview.Id, first, rating: 4, FeedbackRecommendation.Hire);
        await AddFeedbackAsync(tenant, interview.Id, second, rating: 2, FeedbackRecommendation.NoHire);

        var summary = await QueryAsync(tenant, interview.Id, Guid.NewGuid());

        Assert.Equal(2, summary.Items.Count);
        Assert.Equal(2, summary.SubmittedCount);
        Assert.Equal(3d, summary.AverageRating);
        Assert.False(summary.IsWithheld);
        Assert.False(summary.HasCallerSubmitted);
    }

    [Fact]
    public async Task should_withhold_from_an_interviewer_who_has_not_submitted_yet()
    {
        // The rule that matters: the second interviewer must not be able to read the first one's
        // score before committing to their own.
        var tenant = new FixedTenant(Guid.NewGuid());
        var submitted = Guid.NewGuid();
        var pending = Guid.NewGuid();
        var interview = await SeedAsync(tenant, [submitted, pending]);
        await AddFeedbackAsync(tenant, interview.Id, submitted, rating: 5, FeedbackRecommendation.StrongHire);

        var summary = await QueryAsync(tenant, interview.Id, pending);

        Assert.Empty(summary.Items);
        Assert.True(summary.IsWithheld);
        Assert.False(summary.HasCallerSubmitted);
        // The rating must not leak through the aggregate either — an average over one submission
        // would hand over the exact score the items were hidden to protect.
        Assert.Null(summary.AverageRating);
    }

    [Fact]
    public async Task should_still_report_progress_counts_while_withholding()
    {
        // Counts are not a leak, and "1 of 2 in" is what tells someone the panel is still pending.
        var tenant = new FixedTenant(Guid.NewGuid());
        var submitted = Guid.NewGuid();
        var pending = Guid.NewGuid();
        var interview = await SeedAsync(tenant, [submitted, pending]);
        await AddFeedbackAsync(tenant, interview.Id, submitted, rating: 5, FeedbackRecommendation.Hire);

        var summary = await QueryAsync(tenant, interview.Id, pending);

        Assert.True(summary.IsWithheld);
        Assert.Equal(1, summary.SubmittedCount);
        Assert.Equal(2, summary.ExpectedCount);
    }

    [Fact]
    public async Task should_reveal_the_panel_once_the_interviewer_has_submitted()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var interview = await SeedAsync(tenant, [first, second]);
        await AddFeedbackAsync(tenant, interview.Id, first, rating: 5, FeedbackRecommendation.StrongHire);
        await AddFeedbackAsync(tenant, interview.Id, second, rating: 3, FeedbackRecommendation.Hire);

        var summary = await QueryAsync(tenant, interview.Id, second);

        Assert.Equal(2, summary.Items.Count);
        Assert.False(summary.IsWithheld);
        Assert.True(summary.HasCallerSubmitted);
        Assert.Equal(4d, summary.AverageRating);
    }

    [Fact]
    public async Task should_order_feedback_by_submission_time()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var interview = await SeedAsync(tenant, [first, second]);
        await AddFeedbackAsync(tenant, interview.Id, first, rating: 4, FeedbackRecommendation.Hire);
        await AddFeedbackAsync(tenant, interview.Id, second, rating: 4, FeedbackRecommendation.Hire);

        var summary = await QueryAsync(tenant, interview.Id, Guid.NewGuid());

        Assert.True(summary.Items[0].SubmittedAtUtc <= summary.Items[1].SubmittedAtUtc);
    }

    [Fact]
    public async Task should_not_return_another_interviews_feedback()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var target = await SeedAsync(tenant, [Guid.NewGuid()]);
        var other = await SeedAsync(tenant, [Guid.NewGuid()]);
        await AddFeedbackAsync(tenant, other.Id, Guid.NewGuid(), rating: 5, FeedbackRecommendation.Hire);

        var summary = await QueryAsync(tenant, target.Id, Guid.NewGuid());

        Assert.Empty(summary.Items);
        Assert.Equal(0, summary.SubmittedCount);
    }

    [Fact]
    public async Task should_not_reach_across_tenants()
    {
        var owner = new FixedTenant(Guid.NewGuid());
        var stranger = new FixedTenant(Guid.NewGuid());
        var interview = await SeedAsync(owner, [Guid.NewGuid()]);

        var result = await SendAsync(stranger, interview.Id, Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task should_report_a_missing_interview()
    {
        var tenant = new FixedTenant(Guid.NewGuid());

        var result = await SendAsync(tenant, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    private async Task<InterviewFeedbackSummaryDto> QueryAsync(
        FixedTenant tenant, Guid interviewId, Guid callerUserId)
    {
        var result = await SendAsync(tenant, interviewId, callerUserId);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private async Task<Ats.Shared.Kernel.Result<InterviewFeedbackSummaryDto>> SendAsync(
        FixedTenant tenant, Guid interviewId, Guid callerUserId)
    {
        await using var db = NewDb(tenant);
        var handler = new GetInterviewFeedbackHandler(db);
        return await handler.Handle(
            new GetInterviewFeedbackQuery(interviewId, callerUserId), CancellationToken.None);
    }

    private async Task<Interview> SeedAsync(FixedTenant tenant, IReadOnlyCollection<Guid> interviewers)
    {
        // Placed in the past (booked a day ahead of it) so the interview is one that could actually
        // have produced feedback.
        var slot = DateTime.UtcNow.AddHours(-3);
        var interview = Interview.Schedule(
            Guid.NewGuid(), InterviewType.Technical, slot, 60, interviewers,
            nowUtc: slot.AddDays(-1));

        await using var db = NewDb(tenant);
        db.Interviews.Add(interview);
        await db.SaveChangesAsync();
        return interview;
    }

    private async Task AddFeedbackAsync(
        FixedTenant tenant, Guid interviewId, Guid interviewerUserId,
        int rating, FeedbackRecommendation recommendation)
    {
        await using var db = NewDb(tenant);
        db.Feedback.Add(InterviewFeedback.Submit(
            interviewId, interviewerUserId, rating, recommendation, comments: null));
        await db.SaveChangesAsync();
    }

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}
