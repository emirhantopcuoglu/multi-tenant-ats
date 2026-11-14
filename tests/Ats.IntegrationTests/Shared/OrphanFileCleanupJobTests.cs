using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging.Abstractions;

using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.IntegrationTests.Shared;

[Collection("Integration")]
public sealed class OrphanFileCleanupJobTests
{
    private static readonly DateTime TwoDaysAgo = DateTime.UtcNow.AddDays(-2);

    private readonly PostgresContainerFixture _fixture;
    private readonly RecordingFileStorage _storage = new();
    private readonly FixedTenant _tenant = new(Guid.NewGuid());

    public OrphanFileCleanupJobTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task an_old_file_nothing_points_to_should_be_deleted()
    {
        var orphan = Store(TwoDaysAgo);

        await RunAsync();

        Assert.Equal([orphan], _storage.Deleted);
    }

    // The job runs without a tenant, which would hide every application from a filtered query.
    [Fact]
    public async Task an_applications_cv_should_be_kept()
    {
        var key = Store(TwoDaysAgo);
        await SaveApplicationAsync(key, delete: false);

        await RunAsync();

        Assert.Empty(_storage.Deleted);
    }

    // Soft-deleted, so the row is still there; when its CV goes is a retention decision.
    [Fact]
    public async Task a_deleted_applications_cv_should_be_kept()
    {
        var key = Store(TwoDaysAgo);
        await SaveApplicationAsync(key, delete: true);

        await RunAsync();

        Assert.Empty(_storage.Deleted);
    }

    [Fact]
    public async Task a_profile_cv_should_be_kept()
    {
        var key = Store(TwoDaysAgo);
        await using (var db = new CandidateAccountsDbContext(
            PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString)))
        {
            var account = CandidateAccount.Register(
                $"{Guid.NewGuid():N}@example.test", "hash", "Ada", "Applicant", SupportedLanguages.Default);
            account.AttachCv(key, "cv.pdf");
            db.CandidateAccounts.Add(account);
            await db.SaveChangesAsync();
        }

        await RunAsync();

        Assert.Empty(_storage.Deleted);
    }

    // Uploaded a moment before its application is saved.
    [Fact]
    public async Task a_new_file_should_be_kept_even_if_nothing_points_to_it_yet()
    {
        Store(DateTime.UtcNow);

        await RunAsync();

        Assert.Empty(_storage.Deleted);
    }

    [Fact]
    public async Task nothing_should_be_deleted_when_an_owner_cannot_answer()
    {
        Store(TwoDaysAgo);

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(new UnavailableOwner()));

        Assert.Empty(_storage.Deleted);
    }

    // With no owner registered every file would look unused.
    [Fact]
    public async Task nothing_should_be_deleted_when_no_owner_is_registered()
    {
        Store(TwoDaysAgo);

        var job = new OrphanFileCleanupJob(_storage, [], NullLogger<OrphanFileCleanupJob>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => job.CleanupAsync());

        Assert.Empty(_storage.Deleted);
    }

    private string Store(DateTime lastModifiedUtc)
    {
        var key = $"{_tenant.TenantId}/{Guid.NewGuid()}/{Guid.NewGuid()}-cv.pdf";
        _storage.Stored.Add(new StoredFile(key, lastModifiedUtc));
        return key;
    }

    private async Task SaveApplicationAsync(string cvKey, bool delete)
    {
        await using var db = NewApplicationsDb(_tenant);
        var jobId = Guid.NewGuid();
        var pipeline = Pipeline.CreateDefault(jobId);
        var candidate = Candidate.Create($"{Guid.NewGuid():N}@example.test", "Ada", "Applicant");
        var application = ApplicationEntity.Create(
            jobId, candidate.Id, Guid.NewGuid(), pipeline.InitialStage.Id, cvKey);
        db.AddRange(pipeline, candidate, application);
        await db.SaveChangesAsync();

        if (delete)
        {
            db.Applications.Remove(application);
            await db.SaveChangesAsync();
        }
    }

    // Like the Hangfire job: no tenant in scope.
    private async Task RunAsync(params IStoredFileOwner[] extraOwners)
    {
        await using var applicationsDb = NewApplicationsDb(new FixedTenant(null));
        await using var accountsDb = new CandidateAccountsDbContext(
            PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));
        IStoredFileOwner[] owners =
        [
            new ApplicationCvFileOwner(applicationsDb),
            new CandidateAccountCvFileOwner(accountsDb),
            .. extraOwners,
        ];

        await new OrphanFileCleanupJob(_storage, owners, NullLogger<OrphanFileCleanupJob>.Instance).CleanupAsync();
    }

    private ApplicationsDbContext NewApplicationsDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);

    private sealed class UnavailableOwner : IStoredFileOwner
    {
        public Task<IReadOnlyCollection<string>> FindReferencedAsync(
            IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database unavailable");
    }
}
