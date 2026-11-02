using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class ApplicationConcurrencyTests
{
    private readonly PostgresContainerFixture _fixture;

    public ApplicationConcurrencyTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_reject_a_stage_move_based_on_a_copy_read_before_a_concurrent_rejection()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var (application, pipeline) = await SeedActiveApplicationAsync(tenant);
        var interviewStage = pipeline.Stages.Single(s => s.Type == PipelineStageType.Interview);
        var rejectedStage = pipeline.Stages.Single(s => s.Type == PipelineStageType.FinalRejected);

        await using var moverDb = NewDb(tenant);
        await using var rejecterDb = NewDb(tenant);
        var moverCopy = await moverDb.Applications.SingleAsync(a => a.Id == application.Id);
        var rejecterCopy = await rejecterDb.Applications.SingleAsync(a => a.Id == application.Id);

        rejecterCopy.Reject("Salary expectations.", rejectedStage.Id);
        await rejecterDb.SaveChangesAsync();

        moverCopy.MoveToStage(interviewStage.Id);
        var save = () => moverDb.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(save);

        await using var assertDb = NewDb(tenant);
        var stored = await assertDb.Applications.SingleAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Rejected, stored.Status);
        Assert.Equal(rejectedStage.Id, stored.CurrentStageId);
    }

    // Opening an application is a GET; losing the first-view race must not turn it into a 409.
    [Fact]
    public async Task should_not_fail_when_another_user_marks_the_first_view_concurrently()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var (application, _) = await SeedActiveApplicationAsync(tenant);

        await using var handlerDb = NewDb(tenant);
        await handlerDb.Applications.SingleAsync(a => a.Id == application.Id);
        await MarkInOtherContextAsync(tenant, application.Id, a => a.MarkViewed());

        var handler = new MarkApplicationViewedHandler(
            handlerDb, new CapturingPublisher(), new FakeJobDirectory(null), new NullCurrentUser(),
            new InMemoryActivityLog([]), NullLogger<MarkApplicationViewedHandler>.Instance);

        var result = await handler.Handle(new MarkApplicationViewedCommand(application.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task should_not_fail_when_another_user_marks_the_first_cv_download_concurrently()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var (application, _) = await SeedActiveApplicationAsync(tenant);

        await using var handlerDb = NewDb(tenant);
        await handlerDb.Applications.SingleAsync(a => a.Id == application.Id);
        await MarkInOtherContextAsync(tenant, application.Id, a => a.MarkCvDownloaded());

        var handler = new MarkCvDownloadedHandler(handlerDb, new CapturingPublisher(), new FakeJobDirectory(null));

        var result = await handler.Handle(new MarkCvDownloadedCommand(application.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    private async Task MarkInOtherContextAsync(FixedTenant tenant, Guid applicationId, Action<Application> mark)
    {
        await using var otherDb = NewDb(tenant);
        mark(await otherDb.Applications.SingleAsync(a => a.Id == applicationId));
        await otherDb.SaveChangesAsync();
    }

    private async Task<(Application Application, Pipeline Pipeline)> SeedActiveApplicationAsync(
        FixedTenant tenant)
    {
        var jobId = Guid.NewGuid();
        await using var db = NewDb(tenant);
        var pipeline = Pipeline.CreateDefault(jobId);
        db.Pipelines.Add(pipeline);
        var candidate = Candidate.Create($"{Guid.NewGuid():N}@acme.test", "Con", "Current");
        db.Candidates.Add(candidate);
        var application = Application.Create(
            jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/concurrent.pdf");
        db.Applications.Add(application);
        await db.SaveChangesAsync();
        return (application, pipeline);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
