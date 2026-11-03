using Ats.IntegrationTests.Shared;
using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Modules.Interviews.Domain;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Interviews;

[Collection("Integration")]
public sealed class SubmitFeedbackTests
{
    private readonly PostgresContainerFixture _fixture;

    public SubmitFeedbackTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // Parallel submissions can all pass the "already submitted?" check before any of them saves.
    [Fact]
    public async Task Parallel_submissions_by_one_interviewer_should_keep_one_and_refuse_the_rest()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var interviewerId = Guid.NewGuid();
        var interviewId = await SeedPastInterviewAsync(tenant, interviewerId);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => SubmitAsync(tenant, interviewId, interviewerId)));

        Assert.Single(results, result => result.IsSuccess);
        Assert.All(
            results.Where(result => result.IsFailure),
            result => Assert.Equal(InterviewErrors.DuplicateFeedback.Code, result.Error.Code));
        await using var db = NewDb(tenant);
        Assert.Equal(1, await db.Feedback.CountAsync(f => f.InterviewId == interviewId));
    }

    private async Task<Result<Guid>> SubmitAsync(FixedTenant tenant, Guid interviewId, Guid interviewerId)
    {
        await using var db = NewDb(tenant);
        var handler = new SubmitInterviewFeedbackHandler(db);
        return await handler.Handle(
            new SubmitInterviewFeedbackCommand(interviewId, interviewerId, 4, FeedbackRecommendation.Hire, null),
            CancellationToken.None);
    }

    private async Task<Guid> SeedPastInterviewAsync(FixedTenant tenant, Guid interviewerId)
    {
        var bookedAtUtc = DateTime.UtcNow.AddDays(-2);
        var interview = Interview.Schedule(
            Guid.NewGuid(), InterviewType.Technical, bookedAtUtc.AddDays(1), 30, [interviewerId], bookedAtUtc);

        await using var db = NewDb(tenant);
        db.Interviews.Add(interview);
        await db.SaveChangesAsync();
        return interview.Id;
    }

    private InterviewsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildInterviewsOptions(_fixture.ConnectionString, tenant), tenant);
}
