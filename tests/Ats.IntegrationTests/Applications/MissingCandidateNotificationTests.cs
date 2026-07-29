using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Applications;

// Move, reject and hire each load the candidate row to address the notification they send, and each
// skips that notification when the row is gone. Skipping is right: the recruiter's decision stands
// whether or not anyone can be told, and failing the command would strand the application.
//
// Doing it in silence was not. A candidate row can genuinely disappear — Candidate is soft-deletable
// and an erasure removes it from every query while the application survives as the company's record
// — and "I never heard back" left nothing in the logs to confirm or deny.
[Collection("Integration")]
public sealed class MissingCandidateNotificationTests
{
    private readonly PostgresContainerFixture _fixture;

    public MissingCandidateNotificationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Moving_an_application_whose_candidate_is_gone_should_warn()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var (pipeline, application) = await SeedAsync(tenant, eraseCandidate: true);
        var screening = pipeline.Stages.OrderBy(s => s.Order).ElementAt(1);
        var publisher = new CapturingPublisher();
        var logger = new CapturingLogger<MoveApplicationStageHandler>();

        await using var db = NewDb(tenant);
        var handler = new MoveApplicationStageHandler(
            db, publisher, JobDirectoryFor(tenant, application.JobId),
            new NullCurrentUser(), new InMemoryActivityLog([]), logger);

        var result = await handler.Handle(
            new MoveApplicationStageCommand(application.Id, screening.Id), CancellationToken.None);

        // The move succeeds and is committed — only the notification is skipped, and it says so.
        Assert.True(result.IsSuccess);
        Assert.Empty(publisher.Published);
        Assert.True(logger.Warned("no longer exists"));

        await using var assertDb = NewDb(tenant);
        var moved = await assertDb.Applications.SingleAsync(a => a.Id == application.Id);
        Assert.Equal(screening.Id, moved.CurrentStageId);
    }

    [Fact]
    public async Task Rejecting_an_application_whose_candidate_is_gone_should_warn()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var (_, application) = await SeedAsync(tenant, eraseCandidate: true);
        var publisher = new CapturingPublisher();
        var logger = new CapturingLogger<RejectApplicationHandler>();

        await using var db = NewDb(tenant);
        var handler = new RejectApplicationHandler(
            db, publisher, JobDirectoryFor(tenant, application.JobId),
            new NullCurrentUser(), new InMemoryActivityLog([]), logger);

        var result = await handler.Handle(
            new RejectApplicationCommand(application.Id, "Not a fit."), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(publisher.Published);
        Assert.True(logger.Warned("no longer exists"));
    }

    [Fact]
    public async Task Hiring_an_application_whose_candidate_is_gone_should_warn()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var (_, application) = await SeedAsync(tenant, eraseCandidate: true);
        var publisher = new CapturingPublisher();
        var logger = new CapturingLogger<HireApplicationHandler>();

        await using var db = NewDb(tenant);
        var handler = new HireApplicationHandler(
            db, publisher, JobDirectoryFor(tenant, application.JobId),
            new NullCurrentUser(), new InMemoryActivityLog([]), logger);

        var result = await handler.Handle(
            new HireApplicationCommand(application.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(publisher.Published);
        Assert.True(logger.Warned("no longer exists"));
    }

    [Fact]
    public async Task The_ordinary_path_should_still_publish_and_stay_quiet()
    {
        // Guards the obvious regression: a warning that fires when the candidate is present, or an
        // else-branch wired the wrong way round, would make the three tests above pass for the
        // wrong reason.
        var tenant = new FixedTenant(Guid.NewGuid());
        var (_, application) = await SeedAsync(tenant, eraseCandidate: false);
        var publisher = new CapturingPublisher();
        var logger = new CapturingLogger<RejectApplicationHandler>();

        await using var db = NewDb(tenant);
        var handler = new RejectApplicationHandler(
            db, publisher, JobDirectoryFor(tenant, application.JobId),
            new NullCurrentUser(), new InMemoryActivityLog([]), logger);

        var result = await handler.Handle(
            new RejectApplicationCommand(application.Id, "Not a fit."), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(publisher.Published);
        Assert.False(logger.Warned("no longer exists"));
    }

    private static FakeJobDirectory JobDirectoryFor(FixedTenant tenant, Guid jobId) =>
        new(new JobSummary(jobId, "Staff Engineer", "staff-engineer", tenant.TenantId!.Value));

    private async Task<(Pipeline Pipeline, Application Application)> SeedAsync(
        FixedTenant tenant, bool eraseCandidate)
    {
        var jobId = Guid.NewGuid();
        await using var db = NewDb(tenant);

        var pipeline = Pipeline.CreateDefault(jobId);
        db.Pipelines.Add(pipeline);
        var candidate = Candidate.Create($"{Guid.NewGuid():N}@acme.test", "Gone", "Candidate");
        db.Candidates.Add(candidate);
        var application = Application.Create(
            jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/gone.pdf");
        db.Applications.Add(application);
        await db.SaveChangesAsync();

        if (eraseCandidate)
        {
            // Soft delete, which is what erasing a candidate actually does: the row stays on disk
            // but the global query filter hides it from every read, including the handlers'.
            db.Candidates.Remove(candidate);
            await db.SaveChangesAsync();
        }

        return (pipeline, application);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
