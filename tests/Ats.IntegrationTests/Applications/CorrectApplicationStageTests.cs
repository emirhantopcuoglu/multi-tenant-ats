using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class CorrectApplicationStageTests
{
    private readonly PostgresContainerFixture _fixture;

    public CorrectApplicationStageTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_allow_moving_backward_with_a_reason()
    {
        // Arrange — an application sitting in Interview, past its initial stage
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();

        Pipeline pipeline;
        Application application;
        await using (var db = NewDb(tenant))
        {
            pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("correct@acme.test", "Cor", "Rect");
            db.Candidates.Add(candidate);
            var interview = pipeline.Stages.Single(s => s.Name == "Interview");
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), interview.Id, "cv/correct.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var screening = pipeline.Stages.Single(s => s.Name == "Screening");
        var publisher = new CapturingPublisher();
        var activityLog = new InMemoryActivityLog([]);

        // Act — correct it back to Screening, unlike a plain move-stage this must be allowed
        await using var handlerDb = NewDb(tenant);
        var handler = new CorrectApplicationStageHandler(
            handlerDb, new NullCurrentUser(), activityLog,
            NullLogger<CorrectApplicationStageHandler>.Instance);
        var result = await handler.Handle(
            new CorrectApplicationStageCommand(application.Id, screening.Id, "Recruiter misclicked to Interview"),
            CancellationToken.None);

        // Assert — succeeds, the application actually moved, but NO event is published: a
        // correction must never notify the candidate the way a real stage change does
        Assert.True(result.IsSuccess);
        Assert.Empty(publisher.Published);

        await using var assertDb = NewDb(tenant);
        var moved = await assertDb.Applications.FindAsync(application.Id);
        Assert.Equal(screening.Id, moved!.CurrentStageId);

        var logged = Assert.Single(activityLog.Added);
        Assert.Equal(ApplicationActivityType.StageCorrected, logged.ActivityType);
    }

    [Fact]
    public async Task should_refuse_terminal_stages_as_correction_targets()
    {
        // Arrange — terminal stages are still only reachable through hire/reject, even via a correction
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();

        Pipeline pipeline;
        Application application;
        await using (var db = NewDb(tenant))
        {
            pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("correct-terminal@acme.test", "Ter", "Cor");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/correct-terminal.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var rejected = pipeline.Stages.Single(s => s.Name == "Rejected");
        var activityLog = new InMemoryActivityLog([]);

        // Act
        await using var handlerDb = NewDb(tenant);
        var handler = new CorrectApplicationStageHandler(
            handlerDb, new NullCurrentUser(), activityLog,
            NullLogger<CorrectApplicationStageHandler>.Instance);
        var result = await handler.Handle(
            new CorrectApplicationStageCommand(application.Id, rejected.Id, "Wrong stage"),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrors.TerminalStageRequiresDecision.Code, result.Error.Code);
        Assert.Empty(activityLog.Added);
    }

    [Fact]
    public async Task should_refuse_correcting_to_the_same_stage()
    {
        // Arrange
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();

        Pipeline pipeline;
        Application application;
        await using (var db = NewDb(tenant))
        {
            pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("correct-same@acme.test", "Sam", "Eco");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/correct-same.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var activityLog = new InMemoryActivityLog([]);

        // Act — "correct" to the stage it's already in
        await using var handlerDb = NewDb(tenant);
        var handler = new CorrectApplicationStageHandler(
            handlerDb, new NullCurrentUser(), activityLog,
            NullLogger<CorrectApplicationStageHandler>.Instance);
        var result = await handler.Handle(
            new CorrectApplicationStageCommand(application.Id, pipeline.InitialStage.Id, "No-op"),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrors.CannotMoveBackward.Code, result.Error.Code);
        Assert.Empty(activityLog.Added);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
