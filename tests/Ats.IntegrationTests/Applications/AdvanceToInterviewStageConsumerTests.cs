using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class AdvanceToInterviewStageConsumerTests
{
    private readonly PostgresContainerFixture _fixture;

    public AdvanceToInterviewStageConsumerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_advance_an_applied_application_to_the_interview_stage_and_log_it_once()
    {
        // Arrange — a default pipeline, an application sitting at "Applied".
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();
        Pipeline pipeline;
        Application application;
        await using (var db = NewDb(tenant))
        {
            pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("advance@acme.test", "Ad", "Vance");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/advance.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var interviewStage = pipeline.Stages.Single(s => s.Name == "Interview");
        var activityLog = new InMemoryActivityLog([]);

        // Act — simulate an interview being scheduled against the application.
        await using (var db = NewDb(tenant))
        {
            var consumer = new AdvanceToInterviewStageConsumer(
                db, activityLog, NullLogger<AdvanceToInterviewStageConsumer>.Instance);
            await consumer.AdvanceAsync(application.Id, tenant.TenantId!.Value, CancellationToken.None);
        }

        // Assert — the application moved, and exactly one honest StageChanged entry was logged.
        await using (var readDb = NewDb(tenant))
        {
            var stored = readDb.Applications.Single(a => a.Id == application.Id);
            Assert.Equal(interviewStage.Id, stored.CurrentStageId);
        }

        var logged = Assert.Single(activityLog.Added);
        Assert.Equal(ApplicationActivityType.StageChanged, logged.ActivityType);
        Assert.Null(logged.ActorUserId);
    }

    [Fact]
    public async Task should_not_pull_an_application_backwards_when_a_follow_up_interview_is_scheduled()
    {
        // Arrange — an application already moved past Interview, into Offer.
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();
        Pipeline pipeline;
        Application application;
        await using (var db = NewDb(tenant))
        {
            pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("offer@acme.test", "Of", "Fer");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/offer.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var offerStage = pipeline.Stages.Single(s => s.Name == "Offer");
        await using (var db = NewDb(tenant))
        {
            var stored = db.Applications.Single(a => a.Id == application.Id);
            stored.MoveToStage(offerStage.Id);
            await db.SaveChangesAsync();
        }

        var activityLog = new InMemoryActivityLog([]);

        // Act — a follow-up interview is scheduled while the application is already at Offer.
        await using (var db = NewDb(tenant))
        {
            var consumer = new AdvanceToInterviewStageConsumer(
                db, activityLog, NullLogger<AdvanceToInterviewStageConsumer>.Instance);
            await consumer.AdvanceAsync(application.Id, tenant.TenantId!.Value, CancellationToken.None);
        }

        // Assert — still at Offer, nothing logged.
        await using (var readDb = NewDb(tenant))
        {
            var stored = readDb.Applications.Single(a => a.Id == application.Id);
            Assert.Equal(offerStage.Id, stored.CurrentStageId);
        }
        Assert.Empty(activityLog.Added);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
