using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.Applications;

// The job sees every tenant in the shared database, so the assertions only look at this test's own rows.
[Collection("Integration")]
public sealed class ClosedApplicationRetentionJobTests
{
    private const int RetentionMonths = 12;
    private const string Email = "arda.velioglu@example.test";
    private const string FirstName = "Arda";
    private const string LastName = "Velioglu";
    private const string Phone = "+905550104455";
    private const string CoverLetter = "Arda Velioglu enjoys building data pipelines.";
    private const string RejectionReason = "Arda has no Kafka experience yet.";

    private static readonly string[] PersonalValues = [Email, FirstName, LastName, Phone, CoverLetter, RejectionReason];

    private readonly PostgresContainerFixture _fixture;
    private readonly FixedCurrentUser _recruiter = new(Guid.NewGuid(), "recruiter@acme.test");
    private readonly RecordingApplicationDataEraser _interviews = new();
    private readonly InMemoryActivityLog _activityLog = new([]);
    private readonly RecordingCvParseResults _cvParseResults = new();
    private readonly RecordingFileStorage _storage = new();

    public ClosedApplicationRetentionJobTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task should_erase_an_application_closed_longer_ago_than_the_retention_period()
    {
        var seeded = await ApplyAsync(Guid.NewGuid());
        await CloseAsync(seeded, monthsAgo: RetentionMonths + 1);

        await RunJobAsync();

        var application = await LoadApplicationAsync(seeded.ApplicationId);
        Assert.Null(application.CvFileKey);
        Assert.Null(application.CoverLetter);
        Assert.Null(application.RejectionReason);
        Assert.Equal(ApplicationStatus.Rejected, application.Status);
        Assert.Equal(Candidate.AnonymizedFirstName, (await LoadCandidateAsync(seeded.CandidateId)).FirstName);
        Assert.Contains(seeded.ApplicationId, _interviews.Erased);
        Assert.Contains((seeded.TenantId, seeded.ApplicationId), ErasedDocuments());
        Assert.Contains(seeded.CvFileKey, _storage.Deleted);
        Assert.All(await AuditRowsAsync(seeded.TenantId), entry => Assert.DoesNotContain(
            PersonalValues, value => (entry.Before + entry.After).Contains(value, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task should_keep_open_and_recently_closed_applications()
    {
        var open = await ApplyAsync(Guid.NewGuid());
        var recent = await ApplyAsync(Guid.NewGuid());
        await CloseAsync(recent, monthsAgo: RetentionMonths - 1);

        await RunJobAsync();

        Assert.Equal(CoverLetter, (await LoadApplicationAsync(open.ApplicationId)).CoverLetter);
        Assert.Equal(CoverLetter, (await LoadApplicationAsync(recent.ApplicationId)).CoverLetter);
        Assert.DoesNotContain(open.ApplicationId, _interviews.Erased);
        Assert.DoesNotContain(recent.ApplicationId, _interviews.Erased);
    }

    // The company still needs to reach the candidate about the open one.
    [Fact]
    public async Task should_keep_the_candidates_contact_details_while_another_application_still_holds_data()
    {
        var old = await ApplyAsync(Guid.NewGuid());
        await CloseAsync(old, monthsAgo: RetentionMonths + 1);
        await ApplyAgainAsync(old);

        await RunJobAsync();

        Assert.Null((await LoadApplicationAsync(old.ApplicationId)).CoverLetter);
        Assert.Equal(Email, (await LoadCandidateAsync(old.CandidateId)).Email);
    }

    // The row is the job's only record of what is left to do. Erasing it after a module failed would
    // leave that module's data for good.
    [Fact]
    public async Task a_module_failing_should_keep_the_row_for_the_next_run_and_not_stop_the_others()
    {
        var failing = await ApplyAsync(Guid.NewGuid());
        var other = await ApplyAsync(Guid.NewGuid());
        await CloseAsync(failing, monthsAgo: RetentionMonths + 1);
        await CloseAsync(other, monthsAgo: RetentionMonths + 1);
        _interviews.FailFor = failing.ApplicationId;

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunJobAsync());

        Assert.Equal(failing.CvFileKey, (await LoadApplicationAsync(failing.ApplicationId)).CvFileKey);
        Assert.Null((await LoadApplicationAsync(other.ApplicationId)).CvFileKey);
    }

    // Mongo goes before the row: once the row is erased the job never comes back, so a Mongo outage would
    // keep the CV analysis for good.
    [Fact]
    public async Task a_mongo_failure_should_keep_the_row_for_the_next_run()
    {
        var seeded = await ApplyAsync(Guid.NewGuid());
        await CloseAsync(seeded, monthsAgo: RetentionMonths + 1);
        _cvParseResults.DeleteFailure = new TimeoutException("Mongo is down.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunJobAsync());

        Assert.Equal(seeded.CvFileKey, (await LoadApplicationAsync(seeded.ApplicationId)).CvFileKey);
    }

    [Fact]
    public async Task an_erased_application_should_not_be_picked_up_again()
    {
        var seeded = await ApplyAsync(Guid.NewGuid());
        await CloseAsync(seeded, monthsAgo: RetentionMonths + 1);
        await RunJobAsync();
        _interviews.Erased.Clear();

        await RunJobAsync();

        Assert.DoesNotContain(seeded.ApplicationId, _interviews.Erased);
    }

    // Zero would erase every closed application on the next run.
    [Fact]
    public async Task should_refuse_a_retention_period_shorter_than_a_month()
    {
        var seeded = await ApplyAsync(Guid.NewGuid());
        await CloseAsync(seeded, monthsAgo: 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunJobAsync(retentionMonths: 0));

        Assert.Equal(CoverLetter, (await LoadApplicationAsync(seeded.ApplicationId)).CoverLetter);
    }

    private IEnumerable<(Guid TenantId, Guid ApplicationId)> ErasedDocuments() =>
        _activityLog.Erased.SelectMany(e => e.ApplicationIds.Select(id => (e.TenantId, id)));

    private async Task RunJobAsync(int retentionMonths = RetentionMonths)
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        var personalData = new ApplicationPersonalDataEraser(
            db, _activityLog, _cvParseResults, _storage, NullLogger<ApplicationPersonalDataEraser>.Instance);
        var job = new ClosedApplicationRetentionJob(
            db, [_interviews], personalData,
            Options.Create(new RetentionOptions { ClosedApplicationMonths = retentionMonths }),
            NullLogger<ClosedApplicationRetentionJob>.Instance);

        await job.EraseDueApplicationsAsync();
    }

    // Saved inside a tenant, like a real apply request, so the audit log records every value.
    private async Task<Seeded> ApplyAsync(Guid tenantId)
    {
        await using var db = NewDb(tenantId, new FixedCurrentUser(null));
        var candidate = Candidate.Create(Email, FirstName, LastName, Phone);
        db.Candidates.Add(candidate);
        var application = await AddApplicationAsync(db, tenantId, candidate.Id);
        return new Seeded(tenantId, candidate.Id, application.Id, application.CvFileKey!);
    }

    private async Task ApplyAgainAsync(Seeded seeded)
    {
        await using var db = NewDb(seeded.TenantId, new FixedCurrentUser(null));
        await AddApplicationAsync(db, seeded.TenantId, seeded.CandidateId);
    }

    private static async Task<Application> AddApplicationAsync(ApplicationsDbContext db, Guid tenantId, Guid candidateId)
    {
        var application = Application.Create(
            Guid.NewGuid(), candidateId, candidateAccountId: null, Guid.NewGuid(),
            $"{tenantId}/{candidateId}/{Guid.NewGuid()}-arda-velioglu-cv.pdf", CoverLetter);
        db.Applications.Add(application);
        await db.SaveChangesAsync();
        return application;
    }

    // Rejected by a recruiter now, then dated back: the domain always stamps the current time.
    private async Task CloseAsync(Seeded seeded, int monthsAgo)
    {
        await using (var db = NewDb(seeded.TenantId, _recruiter))
        {
            (await db.Applications.SingleAsync(a => a.Id == seeded.ApplicationId)).Reject(RejectionReason, Guid.NewGuid());
            await db.SaveChangesAsync();
        }

        await using var plainDb = NewDb(tenantId: null, new NullCurrentUser());
        var closedAtUtc = DateTime.UtcNow.AddMonths(-monthsAgo).AddMinutes(-1);
        await plainDb.Applications
            .IgnoreQueryFilters()
            .Where(a => a.Id == seeded.ApplicationId)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.ClosedAtUtc, closedAtUtc));
    }

    private async Task<Application> LoadApplicationAsync(Guid applicationId)
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        return await db.Applications.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == applicationId);
    }

    private async Task<Candidate> LoadCandidateAsync(Guid candidateId)
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        return await db.Candidates.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == candidateId);
    }

    private async Task<List<AuditLogEntry>> AuditRowsAsync(Guid tenantId)
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        return await db.Set<AuditLogEntry>().AsNoTracking().Where(e => e.TenantId == tenantId).ToListAsync();
    }

    // Same interceptor order as Program.cs: tenant, auditable fields, then the audit log.
    private ApplicationsDbContext NewDb(Guid? tenantId, ICurrentUser user)
    {
        var tenant = new FixedTenant(tenantId);
        var options = new DbContextOptionsBuilder<ApplicationsDbContext>(
                PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant))
            .AddInterceptors(new AuditLogInterceptor(tenant, user, new HttpContextAccessor()))
            .Options;
        return new ApplicationsDbContext(options, tenant);
    }

    private sealed record Seeded(Guid TenantId, Guid CandidateId, Guid ApplicationId, string CvFileKey);

    private sealed class RecordingApplicationDataEraser : IApplicationDataEraser
    {
        public List<Guid> Erased { get; } = [];

        public Guid? FailFor { get; set; }

        public Task EraseAsync(IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default)
        {
            if (FailFor is { } failing && applicationIds.Contains(failing))
                throw new IOException("Simulated module failure.");

            Erased.AddRange(applicationIds);
            return Task.CompletedTask;
        }
    }
}
