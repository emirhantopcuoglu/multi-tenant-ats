using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.Interviews;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Contracts.Tenants;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class CandidateApplicationDetailTests
{
    private readonly PostgresContainerFixture _fixture;

    public CandidateApplicationDetailTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_return_full_pipeline_and_candidate_safe_timeline_for_own_application()
    {
        // Arrange — a pipeline, an application owned by the account, and an activity log that
        // contains everything internal: an actor, a duplicate view and a rejection reason.
        var accountId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var tenant = new FixedTenant(Guid.NewGuid());

        Pipeline pipeline;
        Application application;
        await using (var db = NewDb(tenant))
        {
            pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);

            var candidate = Candidate.Create("track@acme.test", "Track", "Me");
            db.Candidates.Add(candidate);

            application = Application.Create(
                jobId, candidate.Id, accountId, pipeline.InitialStage.Id, "cv/track.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var screening = pipeline.Stages.Single(s => s.Name == "Screening");
        var baseTime = DateTime.UtcNow.AddDays(-3);
        var activityLog = new InMemoryActivityLog(
        [
            Entry(application.Id, "Submitted", actor: null, "{}", baseTime),
            Entry(application.Id, "Viewed", actor: Guid.NewGuid(), "{}", baseTime.AddHours(2)),
            // A concurrent double-stamp: only the earliest view may surface.
            Entry(application.Id, "Viewed", actor: Guid.NewGuid(), "{}", baseTime.AddHours(3)),
            Entry(application.Id, "StageChanged", actor: Guid.NewGuid(),
                $"{{\"fromStageId\":\"{pipeline.InitialStage.Id}\",\"toStageId\":\"{screening.Id}\"}}",
                baseTime.AddDays(1)),
            Entry(application.Id, "Rejected", actor: Guid.NewGuid(),
                "{\"reason\":\"internal note that must never reach the candidate\"}",
                baseTime.AddDays(2)),
        ]);

        // Act — read under an unrelated tenant: the account, not the tenant, is the scope.
        await using var readDb = NewDb(new FixedTenant(Guid.NewGuid()));
        var handler = new GetCandidateApplicationDetailHandler(
            readDb,
            new FakeJobDirectory(new JobSummary(jobId, "Staff Engineer", "staff-engineer", tenant.TenantId!.Value)),
            new FakeTenantDirectory(new TenantSummary(tenant.TenantId!.Value, "Acme", "acme")),
            activityLog,
            new FakeInterviewDirectory([]));
        var result = await handler.Handle(
            new GetCandidateApplicationDetailQuery(accountId, application.Id), CancellationToken.None);

        // Assert — the full funnel in order, and a timeline with exactly one Viewed entry, the
        // stage move resolved to its name, and the rejection stripped down to type + date.
        Assert.True(result.IsSuccess);
        var detail = result.Value;
        Assert.Equal("Staff Engineer", detail.JobTitle);
        Assert.Equal("Acme", detail.CompanyName);
        Assert.Equal(
            new[] { "Applied", "Screening", "Interview", "Offer", "Hired", "Rejected" },
            detail.PipelineStages.Select(s => s.Name).ToArray());
        Assert.Equal(
            new[] { "Submitted", "Viewed", "StageChanged", "Rejected" },
            detail.Timeline.Select(e => e.Type).ToArray());
        Assert.Equal(baseTime.AddHours(2), detail.Timeline[1].OccurredAtUtc);
        Assert.Equal("Screening", detail.Timeline[2].StageName);
        Assert.Null(detail.Timeline[3].StageName);
    }

    [Fact]
    public async Task should_include_the_applications_scheduled_interviews_in_schedule_order()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var tenant = new FixedTenant(Guid.NewGuid());

        Application application;
        await using (var db = NewDb(tenant))
        {
            var pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("interviewed@acme.test", "In", "Terviewed");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, accountId, pipeline.InitialStage.Id, "cv/interviewed.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var scheduledAt = DateTime.UtcNow.AddDays(3);
        var interview = new CandidateInterviewInfo(
            Guid.NewGuid(), "Technical", scheduledAt, 60, "Google Meet", "Scheduled");

        // Act
        await using var readDb = NewDb(tenant);
        var handler = new GetCandidateApplicationDetailHandler(
            readDb,
            new FakeJobDirectory(new JobSummary(jobId, "Staff Engineer", "staff-engineer", tenant.TenantId!.Value)),
            new FakeTenantDirectory(new TenantSummary(tenant.TenantId!.Value, "Acme", "acme")),
            new InMemoryActivityLog([]),
            new FakeInterviewDirectory([interview]));
        var result = await handler.Handle(
            new GetCandidateApplicationDetailQuery(accountId, application.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var scheduledInterview = Assert.Single(result.Value.Interviews);
        Assert.Equal("Technical", scheduledInterview.Type);
        Assert.Equal(scheduledAt, scheduledInterview.ScheduledAtUtc);
        Assert.Equal("Google Meet", scheduledInterview.Location);
        Assert.Equal("Scheduled", scheduledInterview.Status);
    }

    [Fact]
    public async Task hired_activity_should_surface_in_the_candidate_timeline()
    {
        // Arrange — the happy ending: submitted, then hired.
        var accountId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var tenant = new FixedTenant(Guid.NewGuid());

        Application application;
        await using (var db = NewDb(tenant))
        {
            var pipeline = Pipeline.CreateDefault(jobId);
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("hired@acme.test", "Hip", "Hire");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, accountId, pipeline.InitialStage.Id, "cv/hired.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var baseTime = DateTime.UtcNow.AddDays(-1);
        var activityLog = new InMemoryActivityLog(
        [
            Entry(application.Id, "Submitted", actor: null, "{}", baseTime),
            Entry(application.Id, "Hired", actor: Guid.NewGuid(), "{}", baseTime.AddHours(4)),
        ]);

        // Act
        await using var readDb = NewDb(tenant);
        var handler = new GetCandidateApplicationDetailHandler(
            readDb,
            new FakeJobDirectory(new JobSummary(jobId, "Staff Engineer", "staff-engineer", tenant.TenantId!.Value)),
            new FakeTenantDirectory(new TenantSummary(tenant.TenantId!.Value, "Acme", "acme")),
            activityLog,
            new FakeInterviewDirectory([]));
        var result = await handler.Handle(
            new GetCandidateApplicationDetailQuery(accountId, application.Id), CancellationToken.None);

        // Assert — the hire surfaces as its own entry with just a type and a date
        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { "Submitted", "Hired" },
            result.Value.Timeline.Select(e => e.Type).ToArray());
        Assert.Equal(baseTime.AddHours(4), result.Value.Timeline[1].OccurredAtUtc);
    }

    [Fact]
    public async Task should_return_not_found_for_another_candidates_application()
    {
        // Arrange — an application owned by someone else; probing its id must look identical
        // to querying an id that does not exist at all.
        var tenant = new FixedTenant(Guid.NewGuid());
        Application application;
        await using (var db = NewDb(tenant))
        {
            var pipeline = Pipeline.CreateDefault(Guid.NewGuid());
            db.Pipelines.Add(pipeline);
            var candidate = Candidate.Create("owner@acme.test", "Real", "Owner");
            db.Candidates.Add(candidate);
            application = Application.Create(
                pipeline.JobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, "cv/x.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        // Act — a different account asks for it.
        await using var readDb = NewDb(tenant);
        var handler = new GetCandidateApplicationDetailHandler(
            readDb, new FakeJobDirectory(null), new FakeTenantDirectory(null),
            new InMemoryActivityLog([]), new FakeInterviewDirectory([]));
        var result = await handler.Handle(
            new GetCandidateApplicationDetailQuery(Guid.NewGuid(), application.Id),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrors.NotFound.Code, result.Error.Code);
    }

    private static ActivityLogEntry Entry(
        Guid applicationId, string type, Guid? actor, string payload, DateTime occurredAtUtc) =>
        new(Guid.NewGuid(), applicationId, type, actor, payload, occurredAtUtc);

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}

[Collection("Integration")]
public sealed class MarkApplicationViewedHandlerTests
{
    private readonly PostgresContainerFixture _fixture;

    public MarkApplicationViewedHandlerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_stamp_the_first_view_once_publish_once_and_log_a_single_activity()
    {
        // Arrange
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();
        var candidateAccountId = Guid.NewGuid();
        Application application;
        Candidate candidate;
        await using (var db = NewDb(tenant))
        {
            candidate = Candidate.Create("viewed@acme.test", "Seen", "Once");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, candidateAccountId, Guid.NewGuid(), "cv/v.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var activityLog = new InMemoryActivityLog([]);
        var publisher = new CapturingPublisher();

        // Act — two consecutive opens; only the first may stamp, publish and log.
        await using (var db = NewDb(tenant))
        {
            var handler = new MarkApplicationViewedHandler(
                db, publisher, new FakeJobDirectory(new JobSummary(jobId, "Staff Engineer", "staff-engineer", tenant.TenantId!.Value)),
                new NullCurrentUser(), activityLog,
                NullLogger<MarkApplicationViewedHandler>.Instance);
            var first = await handler.Handle(
                new MarkApplicationViewedCommand(application.Id), CancellationToken.None);
            var second = await handler.Handle(
                new MarkApplicationViewedCommand(application.Id), CancellationToken.None);

            Assert.True(first.IsSuccess);
            Assert.True(first.Value);
            Assert.True(second.IsSuccess);
            Assert.False(second.Value);
        }

        // Assert — the stamp persisted, exactly one event carrying the routing fields, and
        // exactly one Viewed activity.
        await using (var db = NewDb(tenant))
        {
            var stored = db.Applications.Single(a => a.Id == application.Id);
            Assert.NotNull(stored.FirstViewedAtUtc);
        }
        var published = Assert.Single(publisher.Published);
        var viewed = Assert.IsType<ApplicationViewedEvent>(published);
        Assert.Equal(application.Id, viewed.ApplicationId);
        Assert.Equal(jobId, viewed.JobId);
        Assert.Equal("Staff Engineer", viewed.JobTitle);
        Assert.Equal(candidate.Id, viewed.CandidateId);
        Assert.Equal(candidateAccountId, viewed.CandidateAccountId);
        Assert.Equal(tenant.TenantId!.Value, viewed.TenantId);
        Assert.Single(activityLog.Added, a => a.ActivityType == ApplicationActivityType.Viewed);
    }

    [Fact]
    public async Task should_return_not_found_for_an_unknown_application()
    {
        // Arrange
        await using var db = NewDb(new FixedTenant(Guid.NewGuid()));
        var handler = new MarkApplicationViewedHandler(
            db, new CapturingPublisher(), new FakeJobDirectory(null), new NullCurrentUser(),
            new InMemoryActivityLog([]), NullLogger<MarkApplicationViewedHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new MarkApplicationViewedCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrors.NotFound.Code, result.Error.Code);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}

[Collection("Integration")]
public sealed class MarkCvDownloadedHandlerTests
{
    private readonly PostgresContainerFixture _fixture;

    public MarkCvDownloadedHandlerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_stamp_the_first_download_once_and_publish_once()
    {
        // Arrange
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();
        var candidateAccountId = Guid.NewGuid();
        Application application;
        Candidate candidate;
        await using (var db = NewDb(tenant))
        {
            candidate = Candidate.Create("downloaded@acme.test", "Down", "Loaded");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, candidateAccountId, Guid.NewGuid(), "cv/d.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        var publisher = new CapturingPublisher();

        // Act — two consecutive downloads; only the first may stamp and publish.
        await using (var db = NewDb(tenant))
        {
            var handler = new MarkCvDownloadedHandler(
                db, publisher,
                new FakeJobDirectory(new JobSummary(jobId, "Staff Engineer", "staff-engineer", tenant.TenantId!.Value)));
            var first = await handler.Handle(
                new MarkCvDownloadedCommand(application.Id), CancellationToken.None);
            var second = await handler.Handle(
                new MarkCvDownloadedCommand(application.Id), CancellationToken.None);

            Assert.True(first.IsSuccess);
            Assert.True(first.Value);
            Assert.True(second.IsSuccess);
            Assert.False(second.Value);
        }

        // Assert — the stamp persisted and exactly one event carries the routing fields.
        await using (var db = NewDb(tenant))
        {
            var stored = db.Applications.Single(a => a.Id == application.Id);
            Assert.NotNull(stored.FirstCvDownloadedAtUtc);
        }
        var published = Assert.Single(publisher.Published);
        var downloaded = Assert.IsType<ApplicationCvDownloadedEvent>(published);
        Assert.Equal(application.Id, downloaded.ApplicationId);
        Assert.Equal(jobId, downloaded.JobId);
        Assert.Equal("Staff Engineer", downloaded.JobTitle);
        Assert.Equal(candidate.Id, downloaded.CandidateId);
        Assert.Equal(candidateAccountId, downloaded.CandidateAccountId);
        Assert.Equal(tenant.TenantId!.Value, downloaded.TenantId);
    }

    [Fact]
    public async Task should_return_not_found_for_an_unknown_application()
    {
        // Arrange
        await using var db = NewDb(new FixedTenant(Guid.NewGuid()));
        var handler = new MarkCvDownloadedHandler(db, new CapturingPublisher(), new FakeJobDirectory(null));

        // Act
        var result = await handler.Handle(
            new MarkCvDownloadedCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationErrors.NotFound.Code, result.Error.Code);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}

// In-memory stand-in for the MongoDB activity log: reads serve the entries the test seeded,
// writes are captured for assertion. Tenant scoping is not simulated — these tests exercise the
// handlers' mapping and ownership logic, not the store's isolation (that lives in the Mongo
// implementation).
internal sealed class InMemoryActivityLog : IActivityLogRepository
{
    private readonly IReadOnlyList<ActivityLogEntry> _entries;

    public List<ApplicationActivity> Added { get; } = [];

    public InMemoryActivityLog(IReadOnlyList<ActivityLogEntry> entries) => _entries = entries;

    public Task AddAsync(ApplicationActivity activity, CancellationToken cancellationToken = default)
    {
        Added.Add(activity);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActivityLogEntry>> GetByApplicationAsync(
        Guid applicationId, CancellationToken cancellationToken = default) =>
        GetByApplicationAsync(applicationId, Guid.Empty, cancellationToken);

    public Task<IReadOnlyList<ActivityLogEntry>> GetByApplicationAsync(
        Guid applicationId, Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ActivityLogEntry>>(
            _entries.Where(e => e.ApplicationId == applicationId).ToList());
}

internal sealed class FakeJobDirectory : IJobDirectory
{
    private readonly JobSummary? _summary;

    public FakeJobDirectory(JobSummary? summary) => _summary = summary;

    public Task<PublishedJob?> GetPublishedJobBySlugAsync(
        string slug, CancellationToken cancellationToken = default) =>
        Task.FromResult<PublishedJob?>(null);

    public Task<string?> GetJobTitleByIdAsync(
        Guid jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(_summary?.Title);

    public Task<int> CountOpenJobsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<IReadOnlyDictionary<Guid, JobSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken = default)
    {
        var result = _summary is null
            ? new Dictionary<Guid, JobSummary>()
            : new Dictionary<Guid, JobSummary> { [_summary.Id] = _summary };
        return Task.FromResult<IReadOnlyDictionary<Guid, JobSummary>>(result);
    }

    public Task<JobRequirements?> GetJobRequirementsAsync(
        Guid tenantId, Guid jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult<JobRequirements?>(
            _summary is null ? null : new JobRequirements(_summary.Title, ""));
}

internal sealed class FakeTenantDirectory : ITenantDirectory
{
    private readonly TenantSummary? _summary;

    public FakeTenantDirectory(TenantSummary? summary) => _summary = summary;

    public Task<IReadOnlyDictionary<Guid, TenantSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken = default)
    {
        var result = _summary is null
            ? new Dictionary<Guid, TenantSummary>()
            : new Dictionary<Guid, TenantSummary> { [_summary.Id] = _summary };
        return Task.FromResult<IReadOnlyDictionary<Guid, TenantSummary>>(result);
    }

    public Task<IReadOnlyCollection<Guid>> SearchIdsByNameAsync(
        string term, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<Guid>>([]);

    public Task<TenantPublicProfile?> GetPublicProfileBySlugAsync(
        string slug, CancellationToken cancellationToken = default) =>
        Task.FromResult<TenantPublicProfile?>(null);

    public Task<IReadOnlyCollection<Guid>> GetTenantUserIdsAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<Guid>>([]);
}

internal sealed class FakeInterviewDirectory : IInterviewDirectory
{
    private readonly IReadOnlyList<CandidateInterviewInfo> _interviews;

    public FakeInterviewDirectory(IReadOnlyList<CandidateInterviewInfo> interviews) => _interviews = interviews;

    public Task<int> CountUpcomingInterviewsAsync(
        DateTime nowUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<IReadOnlyList<CandidateInterviewInfo>> GetForApplicationAsync(
        Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_interviews);
}
