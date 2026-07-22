using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.Jobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class MoveApplicationStageTests
{
    private readonly PostgresContainerFixture _fixture;

    public MoveApplicationStageTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_publish_enriched_stage_changed_event_on_move()
    {
        // Arrange — a default pipeline with an application sitting at its initial stage
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();
        var candidateAccountId = Guid.NewGuid();

        Pipeline pipeline;
        Application application;
        Candidate candidate;
        await using (var db = NewDb(tenant))
        {
            pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            candidate = Candidate.Create("move@acme.test", "Mova", "Stage");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, candidateAccountId, pipeline.InitialStage.Id, "cv/move.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var screening = pipeline.Stages.Single(s => s.Name == "Screening");
        var publisher = new CapturingPublisher();

        // Act — move Applied -> Screening
        await using var handlerDb = NewDb(tenant);
        var handler = new MoveApplicationStageHandler(
            handlerDb, publisher,
            new FakeJobDirectory(new JobSummary(jobId, "Staff Engineer", "staff-engineer", tenant.TenantId!.Value)),
            new NullCurrentUser(), new InMemoryActivityLog([]),
            NullLogger<MoveApplicationStageHandler>.Instance);
        var result = await handler.Handle(
            new MoveApplicationStageCommand(application.Id, screening.Id), CancellationToken.None);

        // Assert — one event, carrying everything a notification consumer needs: candidate
        // contact, job title and both stage names resolved to human-readable text
        Assert.True(result.IsSuccess);
        var published = Assert.Single(publisher.Published);
        var stageChanged = Assert.IsType<ApplicationStageChangedEvent>(published);
        Assert.Equal(application.Id, stageChanged.ApplicationId);
        Assert.Equal(jobId, stageChanged.JobId);
        Assert.Equal("Staff Engineer", stageChanged.JobTitle);
        Assert.Equal(candidate.Id, stageChanged.CandidateId);
        Assert.Equal(candidateAccountId, stageChanged.CandidateAccountId);
        Assert.Equal("move@acme.test", stageChanged.CandidateEmail);
        Assert.Equal("Mova", stageChanged.CandidateFirstName);
        Assert.Equal(pipeline.InitialStage.Id, stageChanged.FromStageId);
        Assert.Equal("Applied", stageChanged.FromStageName);
        Assert.Equal(screening.Id, stageChanged.ToStageId);
        Assert.Equal("Screening", stageChanged.ToStageName);
        Assert.Equal(tenant.TenantId!.Value, stageChanged.TenantId);
    }

    [Fact]
    public async Task should_not_publish_when_target_stage_is_not_in_pipeline()
    {
        // Arrange
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();

        Application application;
        await using (var db = NewDb(tenant))
        {
            var pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("stuck@acme.test", "Stu", "Ck");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/stuck.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var publisher = new CapturingPublisher();

        // Act — a stage id from nowhere
        await using var handlerDb = NewDb(tenant);
        var handler = new MoveApplicationStageHandler(
            handlerDb, publisher, new FakeJobDirectory(null), new NullCurrentUser(),
            new InMemoryActivityLog([]), NullLogger<MoveApplicationStageHandler>.Instance);
        var result = await handler.Handle(
            new MoveApplicationStageCommand(application.Id, Guid.NewGuid()), CancellationToken.None);

        // Assert — the command fails and no event leaks out for a move that never happened
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrors.StageNotInPipeline.Code, result.Error.Code);
        Assert.Empty(publisher.Published);
    }

    [Theory]
    [InlineData("Hired")]
    [InlineData("Rejected")]
    public async Task should_refuse_terminal_stages_as_move_targets(string terminalStageName)
    {
        // Arrange — terminal stages must be reached through the hire/reject decisions, never a
        // plain move: a move leaves the status Active, which showed candidates "hired"/"rejected"
        // timelines for applications that were still fully in play.
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();

        Pipeline pipeline;
        Application application;
        await using (var db = NewDb(tenant))
        {
            pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("terminal@acme.test", "Ter", "Minal");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/terminal.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var terminalStage = pipeline.Stages.Single(s => s.Name == terminalStageName);
        var publisher = new CapturingPublisher();

        // Act — try to move straight into the terminal stage
        await using var handlerDb = NewDb(tenant);
        var handler = new MoveApplicationStageHandler(
            handlerDb, publisher, new FakeJobDirectory(null), new NullCurrentUser(),
            new InMemoryActivityLog([]), NullLogger<MoveApplicationStageHandler>.Instance);
        var result = await handler.Handle(
            new MoveApplicationStageCommand(application.Id, terminalStage.Id), CancellationToken.None);

        // Assert — refused, nothing published, and the application did not move
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrors.TerminalStageRequiresDecision.Code, result.Error.Code);
        Assert.Empty(publisher.Published);

        await using var assertDb = NewDb(tenant);
        var unchanged = await assertDb.Applications.FindAsync(application.Id);
        Assert.Equal(pipeline.InitialStage.Id, unchanged!.CurrentStageId);
        Assert.Equal(ApplicationStatus.Active, unchanged.Status);
    }

    [Fact]
    public async Task should_refuse_a_backward_move()
    {
        // Arrange — an application already past Applied, sitting in Interview
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();

        Pipeline pipeline;
        Application application;
        await using (var db = NewDb(tenant))
        {
            pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("backward@acme.test", "Back", "Ward");
            db.Candidates.Add(candidate);
            var interview = pipeline.Stages.Single(s => s.Name == "Interview");
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), interview.Id, "cv/backward.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var applied = pipeline.Stages.Single(s => s.Name == "Applied");
        var publisher = new CapturingPublisher();

        // Act — try to move it back to Applied
        await using var handlerDb = NewDb(tenant);
        var handler = new MoveApplicationStageHandler(
            handlerDb, publisher, new FakeJobDirectory(null), new NullCurrentUser(),
            new InMemoryActivityLog([]), NullLogger<MoveApplicationStageHandler>.Instance);
        var result = await handler.Handle(
            new MoveApplicationStageCommand(application.Id, applied.Id), CancellationToken.None);

        // Assert — refused, nothing published, and the application stayed at Interview
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrors.CannotMoveBackward.Code, result.Error.Code);
        Assert.Empty(publisher.Published);

        await using var assertDb = NewDb(tenant);
        var unchanged = await assertDb.Applications.FindAsync(application.Id);
        Assert.Equal(pipeline.Stages.Single(s => s.Name == "Interview").Id, unchanged!.CurrentStageId);
    }

    [Fact]
    public async Task should_refuse_moving_to_the_same_stage()
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
            var candidate = Candidate.Create("stuck-same@acme.test", "Sta", "Ay");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/same.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var publisher = new CapturingPublisher();

        // Act — "move" to the stage it's already in
        await using var handlerDb = NewDb(tenant);
        var handler = new MoveApplicationStageHandler(
            handlerDb, publisher, new FakeJobDirectory(null), new NullCurrentUser(),
            new InMemoryActivityLog([]), NullLogger<MoveApplicationStageHandler>.Instance);
        var result = await handler.Handle(
            new MoveApplicationStageCommand(application.Id, pipeline.InitialStage.Id), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrors.CannotMoveBackward.Code, result.Error.Code);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task should_allow_skipping_ahead_to_a_later_stage()
    {
        // Arrange — an application at Applied, its pipeline's very first stage
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();

        Pipeline pipeline;
        Application application;
        await using (var db = NewDb(tenant))
        {
            pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("skip@acme.test", "Ski", "Pah");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/skip.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var offer = pipeline.Stages.Single(s => s.Name == "Offer");
        var publisher = new CapturingPublisher();

        // Act — jump straight to Offer, skipping Screening and Interview
        await using var handlerDb = NewDb(tenant);
        var handler = new MoveApplicationStageHandler(
            handlerDb, publisher, new FakeJobDirectory(null), new NullCurrentUser(),
            new InMemoryActivityLog([]), NullLogger<MoveApplicationStageHandler>.Instance);
        var result = await handler.Handle(
            new MoveApplicationStageCommand(application.Id, offer.Id), CancellationToken.None);

        // Assert — a forward skip is a legitimate business decision, not a mistake, so it succeeds
        Assert.True(result.IsSuccess);
        var published = Assert.Single(publisher.Published);
        Assert.IsType<ApplicationStageChangedEvent>(published);

        await using var assertDb = NewDb(tenant);
        var moved = await assertDb.Applications.FindAsync(application.Id);
        Assert.Equal(offer.Id, moved!.CurrentStageId);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
