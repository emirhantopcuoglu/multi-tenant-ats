using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.Jobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class RejectApplicationTests
{
    private readonly PostgresContainerFixture _fixture;

    public RejectApplicationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_reject_and_park_in_final_rejected_stage()
    {
        // Arrange — an active application sitting at the initial stage
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();

        Pipeline pipeline;
        Application application;
        await using (var db = NewDb(tenant))
        {
            pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("reject@acme.test", "Reg", "Ect");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/reject.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var rejectedStage = pipeline.Stages.Single(s => s.Type == PipelineStageType.FinalRejected);
        var publisher = new CapturingPublisher();

        // Act
        await using var handlerDb = NewDb(tenant);
        var handler = new RejectApplicationHandler(
            handlerDb, publisher,
            new FakeJobDirectory(new JobSummary(jobId, "Staff Engineer", "staff-engineer", tenant.TenantId!.Value)),
            new NullCurrentUser(), new InMemoryActivityLog([]),
            NullLogger<RejectApplicationHandler>.Instance);
        var result = await handler.Handle(
            new RejectApplicationCommand(application.Id, "Not a fit."), CancellationToken.None);

        // Assert — the status and the stage tell the same story
        Assert.True(result.IsSuccess);

        await using var assertDb = NewDb(tenant);
        var rejected = await assertDb.Applications.FindAsync(application.Id);
        Assert.Equal(ApplicationStatus.Rejected, rejected!.Status);
        Assert.Equal(rejectedStage.Id, rejected.CurrentStageId);

        var published = Assert.Single(publisher.Published);
        Assert.IsType<ApplicationRejectedEvent>(published);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
