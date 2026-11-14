using System.Net;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class ApplicationCandidateDataEraserTests
{
    private const string Email = "mira.tolun@example.test";
    private const string FirstName = "Mira";
    private const string LastName = "Tolun";
    private const string Phone = "+905550102030";
    private const string LinkedIn = "https://www.linkedin.com/in/mira-tolun-test";
    private const string CoverLetter = "Mira Tolun would love to build your hiring tools.";
    private const string RejectionReason = "Mira lacks distributed systems experience.";
    private const string CandidateIp = "198.51.100.23";
    private const string CandidateBrowser = "MiraBrowser/1.0";
    private const string RecruiterIp = "203.0.113.7";
    private const string RecruiterBrowser = "OfficeBrowser/2.0";

    private static readonly string[] PersonalValues =
        [Email, FirstName, LastName, Phone, LinkedIn, CoverLetter, RejectionReason, CandidateIp, CandidateBrowser];

    private readonly PostgresContainerFixture _fixture;
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly FixedCurrentUser _recruiter = new(Guid.NewGuid(), "recruiter@acme.test");
    private readonly CapturingPublisher _publisher = new();
    private readonly InMemoryActivityLog _activityLog = new([]);
    private readonly RecordingCvParseResults _cvParseResults = new();

    public ApplicationCandidateDataEraserTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task should_anonymize_the_candidate_in_every_company_they_applied_to()
    {
        var first = await ApplyAsync(Guid.NewGuid());
        var second = await ApplyAsync(Guid.NewGuid());
        await RejectAsync(first);
        await RejectAsync(second);

        await EraseAsync();

        foreach (var seeded in new[] { first, second })
        {
            var candidate = await LoadCandidateAsync(seeded.CandidateId);
            Assert.Equal(AnonymizedEmail.For(seeded.CandidateId), candidate.Email);
            Assert.Equal(Candidate.AnonymizedFirstName, candidate.FirstName);
            Assert.Null(candidate.Phone);
            Assert.Null(candidate.LinkedInUrl);

            var application = await LoadApplicationAsync(seeded.ApplicationId);
            Assert.Null(application.CvFileKey);
            Assert.Null(application.CoverLetter);
            Assert.Null(application.RejectionReason);
            Assert.Equal(ApplicationStatus.Rejected, application.Status);
        }
    }

    // The company can no longer reach the candidate, and its upcoming interviews must be cancelled.
    [Fact]
    public async Task should_close_an_active_application_the_way_a_withdrawal_does()
    {
        var seeded = await ApplyAsync(Guid.NewGuid());

        await EraseAsync();

        Assert.Equal(ApplicationStatus.Withdrawn, (await LoadApplicationAsync(seeded.ApplicationId)).Status);
        var withdrawn = Assert.Single(_publisher.Published.OfType<ApplicationWithdrawnEvent>());
        Assert.Equal(new ApplicationWithdrawnEvent(seeded.ApplicationId, seeded.TenantId), withdrawn);
        var logged = Assert.Single(_activityLog.Added);
        Assert.Equal(ApplicationActivityType.Withdrawn, logged.ActivityType);
        Assert.Equal(seeded.TenantId, Assert.Single(_activityLog.AddedTenantIds));
    }

    [Fact]
    public async Task should_not_announce_anything_for_an_application_that_was_already_closed()
    {
        var seeded = await ApplyAsync(Guid.NewGuid());
        await RejectAsync(seeded);

        await EraseAsync();

        Assert.Empty(_publisher.Published);
        Assert.Empty(_activityLog.Added);
    }

    [Fact]
    public async Task should_reach_an_application_the_company_deleted()
    {
        var seeded = await ApplyAsync(Guid.NewGuid());
        await DeleteAsync(seeded);

        await EraseAsync();

        Assert.Null((await LoadApplicationAsync(seeded.ApplicationId)).CoverLetter);
        Assert.Equal(Candidate.AnonymizedFirstName, (await LoadCandidateAsync(seeded.CandidateId)).FirstName);
    }

    [Fact]
    public async Task should_leave_other_candidates_untouched()
    {
        var tenantId = Guid.NewGuid();
        await ApplyAsync(tenantId);
        var other = await ApplyAsync(tenantId, Guid.NewGuid(), "deniz.kaya@example.test");

        await EraseAsync();

        Assert.Equal("deniz.kaya@example.test", (await LoadCandidateAsync(other.CandidateId)).Email);
        Assert.Equal(CoverLetter, (await LoadApplicationAsync(other.ApplicationId)).CoverLetter);
        Assert.All(
            (await AuditRowsAsync(tenantId)).Where(e => e.ResourceId == other.ApplicationId.ToString()),
            entry => Assert.NotNull(entry.After));
    }

    [Fact]
    public async Task should_delete_the_cv_files()
    {
        var first = await ApplyAsync(Guid.NewGuid());
        var second = await ApplyAsync(Guid.NewGuid());
        var storage = new RecordingFileStorage();

        await EraseAsync(storage);

        Assert.Equal(new[] { first.CvFileKey, second.CvFileKey }.Order(), storage.Deleted.Order());
    }

    // No row points to the files anymore, so the nightly orphan file cleanup removes them.
    [Fact]
    public async Task a_storage_outage_should_not_fail_the_erasure()
    {
        var seeded = await ApplyAsync(Guid.NewGuid());

        await EraseAsync(new RecordingFileStorage { DeleteFailure = new IOException("storage down") });

        Assert.Null((await LoadApplicationAsync(seeded.ApplicationId)).CvFileKey);
        Assert.Single(_cvParseResults.Deleted);
    }

    // Who did what and when stays; the values and the candidate's request details go.
    [Fact]
    public async Task should_clear_the_old_values_and_the_candidates_request_details_from_the_audit_log()
    {
        var seeded = await ApplyAsync(Guid.NewGuid());
        await RejectAsync(seeded);
        var rowsBefore = await AuditRowsAsync(seeded.TenantId);

        await EraseAsync();

        var rows = await AuditRowsAsync(seeded.TenantId);
        Assert.Equal(rowsBefore.Select(e => (e.Id, e.Action, e.ActorUserId)).Order(), rows.Select(e => (e.Id, e.Action, e.ActorUserId)).Order());
        Assert.All(rows, entry => Assert.DoesNotContain(PersonalValues, value => Mentions(entry, value)));
    }

    // The erasure runs with no tenant, so the audit interceptor writes nothing for it. A row for it would
    // copy every old value back.
    [Fact]
    public async Task should_not_record_the_erasure_itself_in_the_audit_log()
    {
        var seeded = await ApplyAsync(Guid.NewGuid());
        await RejectAsync(seeded);
        var countBefore = (await AuditRowsAsync(seeded.TenantId)).Count;

        await EraseAsync();

        Assert.Equal(countBefore, (await AuditRowsAsync(seeded.TenantId)).Count);
    }

    [Fact]
    public async Task should_erase_the_activity_log_and_the_cv_analysis_of_each_company()
    {
        var first = await ApplyAsync(Guid.NewGuid());
        var second = await ApplyAsync(Guid.NewGuid());

        await EraseAsync();

        var expected = new[] { (first.TenantId, first.ApplicationId), (second.TenantId, second.ApplicationId) }.Order();
        Assert.Equal(expected, _activityLog.Erased.SelectMany(e => e.ApplicationIds.Select(id => (e.TenantId, id))).Order());
        Assert.Equal(expected, _cvParseResults.Deleted.SelectMany(e => e.ApplicationIds.Select(id => (e.TenantId, id))).Order());
    }

    // A failed run is retried from the start the next night.
    [Fact]
    public async Task running_it_again_should_change_nothing()
    {
        var seeded = await ApplyAsync(Guid.NewGuid());
        var storage = new RecordingFileStorage();
        await EraseAsync(storage);

        await EraseAsync(storage);

        Assert.Single(_publisher.Published);
        Assert.Single(storage.Deleted);
        Assert.Equal(ApplicationStatus.Withdrawn, (await LoadApplicationAsync(seeded.ApplicationId)).Status);
    }

    // The deletion job reuses one instance for every due account.
    [Fact]
    public async Task a_failed_erasure_should_not_spill_into_the_next_account()
    {
        var failing = await ApplyAsync(Guid.NewGuid());
        var nextAccountId = Guid.NewGuid();
        var next = await ApplyAsync(Guid.NewGuid(), nextAccountId, "deniz.kaya@example.test");
        await using var db = NewDb(tenantId: null, new NullCurrentUser(), interceptor: new FailSavesOf(failing.ApplicationId));
        var eraser = CreateEraser(db, new RecordingFileStorage());

        await Assert.ThrowsAsync<DbUpdateException>(() => eraser.EraseAsync(_accountId));
        await eraser.EraseAsync(nextAccountId);

        Assert.Equal(CoverLetter, (await LoadApplicationAsync(failing.ApplicationId)).CoverLetter);
        Assert.Null((await LoadApplicationAsync(next.ApplicationId)).CoverLetter);
    }

    private static bool Mentions(AuditLogEntry entry, string value) =>
        new[] { entry.Before, entry.After, entry.ActorEmail, entry.IpAddress, entry.UserAgent }
            .Any(field => field?.Contains(value, StringComparison.OrdinalIgnoreCase) == true);

    private async Task EraseAsync(RecordingFileStorage? storage = null)
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        await CreateEraser(db, storage ?? new RecordingFileStorage()).EraseAsync(_accountId);
    }

    private ApplicationCandidateDataEraser CreateEraser(ApplicationsDbContext db, RecordingFileStorage storage) =>
        new(db, _publisher, _activityLog,
            new ApplicationPersonalDataEraser(
                db, _activityLog, _cvParseResults, storage, NullLogger<ApplicationPersonalDataEraser>.Instance),
            NullLogger<ApplicationCandidateDataEraser>.Instance);

    // Saved the way a candidate's apply request saves it, so the audit log holds their details.
    private async Task<Seeded> ApplyAsync(Guid tenantId, Guid? accountId = null, string email = Email)
    {
        var candidateAccountId = accountId ?? _accountId;
        await using var db = NewDb(tenantId, new FixedCurrentUser(candidateAccountId, email), CandidateIp, CandidateBrowser);
        var candidate = Candidate.Create(email, FirstName, LastName, Phone, LinkedIn);
        var cvFileKey = $"{tenantId}/{candidate.Id}/{Guid.NewGuid()}-mira-tolun-cv.pdf";
        var application = Application.Create(
            Guid.NewGuid(), candidate.Id, candidateAccountId, Guid.NewGuid(), cvFileKey, CoverLetter);
        db.Candidates.Add(candidate);
        db.Applications.Add(application);
        await db.SaveChangesAsync();
        return new Seeded(tenantId, candidate.Id, application.Id, cvFileKey);
    }

    private async Task RejectAsync(Seeded seeded)
    {
        await using var db = NewDb(seeded.TenantId, _recruiter);
        (await db.Applications.SingleAsync(a => a.Id == seeded.ApplicationId)).Reject(RejectionReason, Guid.NewGuid());
        await db.SaveChangesAsync();
    }

    private async Task DeleteAsync(Seeded seeded)
    {
        await using var db = NewDb(seeded.TenantId, _recruiter);
        db.Applications.Remove(await db.Applications.SingleAsync(a => a.Id == seeded.ApplicationId));
        await db.SaveChangesAsync();
    }

    private async Task<Candidate> LoadCandidateAsync(Guid candidateId)
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        return await db.Candidates.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == candidateId);
    }

    private async Task<Application> LoadApplicationAsync(Guid applicationId)
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        return await db.Applications.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == applicationId);
    }

    private async Task<List<AuditLogEntry>> AuditRowsAsync(Guid tenantId)
    {
        await using var db = NewDb(tenantId: null, new NullCurrentUser());
        return await db.Set<AuditLogEntry>().AsNoTracking().Where(e => e.TenantId == tenantId).ToListAsync();
    }

    // Same interceptor order as Program.cs: tenant, auditable fields, then the audit log.
    private ApplicationsDbContext NewDb(
        Guid? tenantId,
        ICurrentUser user,
        string ipAddress = RecruiterIp,
        string browser = RecruiterBrowser,
        IInterceptor? interceptor = null)
    {
        var tenant = new FixedTenant(tenantId);
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);
        httpContext.Request.Headers.UserAgent = browser;
        var options = new DbContextOptionsBuilder<ApplicationsDbContext>(
                PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant))
            .AddInterceptors(new AuditLogInterceptor(tenant, user, new HttpContextAccessor { HttpContext = httpContext }))
            .AddInterceptors(interceptor is null ? [] : [interceptor])
            .Options;
        return new ApplicationsDbContext(options, tenant);
    }

    private sealed record Seeded(Guid TenantId, Guid CandidateId, Guid ApplicationId, string CvFileKey);

    // A save that changes this application always fails, the way a broken row would.
    private sealed class FailSavesOf : SaveChangesInterceptor
    {
        private readonly Guid _applicationId;

        public FailSavesOf(Guid applicationId) => _applicationId = applicationId;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var touchesApplication = eventData.Context!.ChangeTracker.Entries<Application>()
                .Any(entry => entry.Entity.Id == _applicationId && entry.State == EntityState.Modified);

            return touchesApplication
                ? throw new DbUpdateException("Simulated failure.")
                : ValueTask.FromResult(result);
        }
    }
}

internal sealed class RecordingCvParseResults : ICvParseResultRepository
{
    public List<(Guid TenantId, IReadOnlyCollection<Guid> ApplicationIds)> Deleted { get; } = [];

    public Exception? DeleteFailure { get; set; }

    public Task SaveAsync(
        Guid tenantId, Guid applicationId, CvParseResult result, DateTime parsedAtUtc,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<StoredCvParseResult?> GetByApplicationAsync(
        Guid applicationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<StoredCvParseResult?>(null);

    public Task DeleteAsync(
        Guid tenantId, IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default)
    {
        if (DeleteFailure is not null)
            throw DeleteFailure;

        Deleted.Add((tenantId, applicationIds));
        return Task.CompletedTask;
    }
}
