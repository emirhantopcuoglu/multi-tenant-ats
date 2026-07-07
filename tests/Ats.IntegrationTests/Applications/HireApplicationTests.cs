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
public sealed class HireApplicationTests
{
    private readonly PostgresContainerFixture _fixture;

    public HireApplicationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_hire_park_in_final_hired_stage_and_publish_enriched_event()
    {
        // Arrange — an active application sitting mid-funnel
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
            candidate = Candidate.Create("hire@acme.test", "Hira", "Ble");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, candidateAccountId, pipeline.InitialStage.Id, "cv/hire.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var hiredStage = pipeline.Stages.Single(s => s.Type == PipelineStageType.FinalHired);
        var publisher = new CapturingPublisher();
        var activityLog = new InMemoryActivityLog([]);

        // Act
        await using var handlerDb = NewDb(tenant);
        var handler = new HireApplicationHandler(
            handlerDb, publisher,
            new FakeJobDirectory(new JobSummary(jobId, "Staff Engineer", "staff-engineer", tenant.TenantId!.Value)),
            new NullCurrentUser(), activityLog,
            NullLogger<HireApplicationHandler>.Instance);
        var result = await handler.Handle(
            new HireApplicationCommand(application.Id), CancellationToken.None);

        // Assert — status and stage flip together, and the event carries what the email needs
        Assert.True(result.IsSuccess);

        await using var assertDb = NewDb(tenant);
        var hired = await assertDb.Applications.FindAsync(application.Id);
        Assert.Equal(ApplicationStatus.Hired, hired!.Status);
        Assert.Equal(hiredStage.Id, hired.CurrentStageId);

        var published = Assert.Single(publisher.Published);
        var hiredEvent = Assert.IsType<ApplicationHiredEvent>(published);
        Assert.Equal(application.Id, hiredEvent.ApplicationId);
        Assert.Equal("Staff Engineer", hiredEvent.JobTitle);
        Assert.Equal("hire@acme.test", hiredEvent.CandidateEmail);
        Assert.Equal("Hira", hiredEvent.CandidateFirstName);
        Assert.Equal(tenant.TenantId!.Value, hiredEvent.TenantId);

        var activity = Assert.Single(activityLog.Added);
        Assert.Equal(ApplicationActivityType.Hired, activity.ActivityType);
    }

    [Fact]
    public async Task should_fail_when_application_is_already_terminal()
    {
        // Arrange — a rejected application cannot be hired afterwards
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();

        Application application;
        await using (var db = NewDb(tenant))
        {
            var pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("late@acme.test", "Too", "Late");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/late.pdf");
            application.Reject("Position filled.", Guid.NewGuid());
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var publisher = new CapturingPublisher();

        // Act
        await using var handlerDb = NewDb(tenant);
        var handler = new HireApplicationHandler(
            handlerDb, publisher, new FakeJobDirectory(null), new NullCurrentUser(),
            new InMemoryActivityLog([]), NullLogger<HireApplicationHandler>.Instance);
        var result = await handler.Handle(
            new HireApplicationCommand(application.Id), CancellationToken.None);

        // Assert — the entity's terminal guard surfaces as a typed failure, nothing leaks out
        Assert.False(result.IsSuccess);
        Assert.Equal("application.invalid_operation", result.Error.Code);
        Assert.Empty(publisher.Published);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
