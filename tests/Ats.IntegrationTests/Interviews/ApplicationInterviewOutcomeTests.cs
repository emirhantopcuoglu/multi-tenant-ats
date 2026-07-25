using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;

namespace Ats.IntegrationTests.Interviews;

// The roll-up the application screen uses to decide what to do next. Before this, feedback existed
// only per interview, so deciding meant opening each one and holding the scores in your head.
[Collection("Integration")]
public sealed class ApplicationInterviewOutcomeTests
{
    private readonly PostgresContainerFixture _fixture;

    public ApplicationInterviewOutcomeTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_be_empty_for_an_application_with_no_interviews()
    {
        var tenant = new FixedTenant(Guid.NewGuid());

        var outcome = await QueryAsync(tenant, Guid.NewGuid());

        Assert.Equal(0, outcome.TotalCount);
        Assert.Null(outcome.AverageRating);
        Assert.Empty(outcome.RecommendationCounts);
    }

    [Fact]
    public async Task should_aggregate_ratings_and_recommendations_across_interviews()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var first = await SeedElapsedAsync(tenant, applicationId, [Guid.NewGuid()]);
        var second = await SeedElapsedAsync(tenant, applicationId, [Guid.NewGuid()]);
        await AddFeedbackAsync(tenant, first.Id, 5, FeedbackRecommendation.StrongHire);
        await AddFeedbackAsync(tenant, second.Id, 3, FeedbackRecommendation.Hire);

        var outcome = await QueryAsync(tenant, applicationId);

        Assert.Equal(2, outcome.TotalCount);
        Assert.Equal(2, outcome.FeedbackCount);
        Assert.Equal(4d, outcome.AverageRating);
        Assert.Equal(1, outcome.RecommendationCounts["StrongHire"]);
        Assert.Equal(1, outcome.RecommendationCounts["Hire"]);
    }

    [Fact]
    public async Task should_count_elapsed_interviews_as_awaiting_an_outcome()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        await SeedElapsedAsync(tenant, applicationId, [Guid.NewGuid()]);

        var outcome = await QueryAsync(tenant, applicationId);

        Assert.Equal(1, outcome.AwaitingOutcomeCount);
        Assert.Equal(0, outcome.CompletedCount);
    }

    [Fact]
    public async Task should_expect_one_feedback_per_interviewer_on_interviews_that_happened()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        await SeedElapsedAsync(tenant, applicationId, [Guid.NewGuid(), Guid.NewGuid()]);

        var outcome = await QueryAsync(tenant, applicationId);

        Assert.Equal(2, outcome.ExpectedFeedbackCount);
        Assert.Equal(0, outcome.FeedbackCount);
    }

    [Fact]
    public async Task should_not_expect_feedback_from_a_cancelled_interview()
    {
        // Nobody was there to give it. Counting it would leave a complete panel looking permanently
        // one evaluation short.
        var tenant = new FixedTenant(Guid.NewGuid());
        var applicationId = Guid.NewGuid();
        var slot = DateTime.UtcNow.AddDays(2);
        var interview = await SeedAsync(tenant, applicationId, slot, [Guid.NewGuid()]);

        await using (var db = NewDb(tenant))
        {
            var tracked = await db.Interviews.FindAsync(interview.Id);
            tracked!.Cancel(InterviewCancellationReason.PositionClosed, null, DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        var outcome = await QueryAsync(tenant, applicationId);

        Assert.Equal(1, outcome.TotalCount);
        Assert.Equal(0, outcome.ExpectedFeedbackCount);
    }

    [Fact]
    public async Task should_not_mix_in_another_applications_interviews()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var target = Guid.NewGuid();
        await SeedElapsedAsync(tenant, target, [Guid.NewGuid()]);
        var otherInterview = await SeedElapsedAsync(tenant, Guid.NewGuid(), [Guid.NewGuid()]);
        await AddFeedbackAsync(tenant, otherInterview.Id, 1, FeedbackRecommendation.NoHire);

        var outcome = await QueryAsync(tenant, target);

        Assert.Equal(1, outcome.TotalCount);
        Assert.Equal(0, outcome.FeedbackCount);
        Assert.Null(outcome.AverageRating);
    }

    private async Task<ApplicationInterviewOutcomeDto> QueryAsync(
        FixedTenant tenant, Guid applicationId)
    {
        await using var db = NewDb(tenant);
        var handler = new GetApplicationInterviewOutcomeHandler(db);
        var result = await handler.Handle(
            new GetApplicationInterviewOutcomeQuery(applicationId), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private Task<Interview> SeedElapsedAsync(
        FixedTenant tenant, Guid applicationId, IReadOnlyCollection<Guid> interviewers) =>
        SeedAsync(tenant, applicationId, DateTime.UtcNow.AddHours(-3), interviewers);

    private async Task<Interview> SeedAsync(
        FixedTenant tenant, Guid applicationId, DateTime slot, IReadOnlyCollection<Guid> interviewers)
    {
        var interview = Interview.Schedule(
            applicationId, InterviewType.Technical, slot, 60, interviewers, nowUtc: slot.AddDays(-1));

        await using var db = NewDb(tenant);
        db.Interviews.Add(interview);
        await db.SaveChangesAsync();
        return interview;
    }

    private async Task AddFeedbackAsync(
        FixedTenant tenant, Guid interviewId, int rating, FeedbackRecommendation recommendation)
    {
        await using var db = NewDb(tenant);
        db.Feedback.Add(InterviewFeedback.Submit(
            interviewId, Guid.NewGuid(), rating, recommendation, comments: null));
        await db.SaveChangesAsync();
    }

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}
