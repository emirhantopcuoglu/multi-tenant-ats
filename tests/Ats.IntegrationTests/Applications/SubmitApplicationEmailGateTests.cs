using System.Text;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Applications;

// Applying is the single action gated on a verified email address. Everything else a candidate can do
// affects only their own account; from here on a recruiter reads that address, writes to it and
// schedules time around it.
[Collection("Integration")]
public sealed class SubmitApplicationEmailGateTests
{
    private readonly PostgresContainerFixture _fixture;

    public SubmitApplicationEmailGateTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_refuse_an_unverified_candidate_without_uploading_the_cv()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var storage = new RecordingFileStorage();

        // Act
        var result = await SubmitAsync(tenantId, storage, isEmailVerified: false);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.EmailNotVerified.Code, result.Error.Code);

        // The gate runs BEFORE the upload on purpose: a refused application must not leave a CV — real
        // personal data — sitting in object storage with no row referencing it and nothing to clean it
        // up. Asserting on the storage rather than only the error is what pins that ordering.
        Assert.Empty(storage.Uploaded);
        Assert.Equal(0, await CountApplicationsAsync(tenantId));
    }

    [Fact]
    public async Task should_accept_a_verified_candidate()
    {
        // The counterpart: without this, deleting the gate entirely would still leave the test above
        // failing for the wrong reason, and nothing would prove the happy path still works.
        var tenantId = Guid.NewGuid();
        var storage = new RecordingFileStorage();

        var result = await SubmitAsync(tenantId, storage, isEmailVerified: true);

        Assert.True(result.IsSuccess);
        Assert.Single(storage.Uploaded);
        Assert.Equal(1, await CountApplicationsAsync(tenantId));
    }

    private async Task<Result<Guid>> SubmitAsync(
        Guid tenantId, IFileStorage storage, bool isEmailVerified)
    {
        var tenant = new FixedTenant(tenantId);
        var accountId = Guid.NewGuid();
        var job = new PublishedJob(Guid.NewGuid(), "Staff Engineer", "staff-engineer");

        await using var db = NewDb(tenant);
        var handler = new SubmitApplicationHandler(
            db,
            new StubPublishedJobDirectory(job),
            new StubCandidateAccountReader(new CandidateAccountSummary(
                accountId, $"{Guid.NewGuid():N}@acme.test", "Test", "Candidate", isEmailVerified)),
            storage,
            tenant,
            new CapturingPublisher(),
            new InMemoryActivityLog([]),
            NullLogger<SubmitApplicationHandler>.Instance);

        var cv = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 pretend cv"));
        return await handler.Handle(
            new SubmitApplicationCommand(
                JobSlug: job.Slug,
                CandidateAccountId: accountId,
                Phone: null,
                LinkedInUrl: null,
                CoverLetter: null,
                CvContent: cv,
                CvSizeBytes: cv.Length,
                CvContentType: "application/pdf",
                CvFileName: "cv.pdf"),
            CancellationToken.None);
    }

    private async Task<int> CountApplicationsAsync(Guid tenantId)
    {
        await using var db = NewDb(new FixedTenant(tenantId));
        return await db.Applications.CountAsync();
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}

// Unlike FakeJobDirectory, this one actually resolves a published job by slug — that lookup is the
// step SubmitApplicationHandler depends on, and the other fake deliberately returns null for it.
internal sealed class StubPublishedJobDirectory : IJobDirectory
{
    private readonly PublishedJob _job;

    public StubPublishedJobDirectory(PublishedJob job) => _job = job;

    public Task<PublishedJob?> GetPublishedJobBySlugAsync(
        string slug, CancellationToken cancellationToken = default) =>
        Task.FromResult<PublishedJob?>(slug == _job.Slug ? _job : null);

    public Task<string?> GetJobTitleByIdAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(jobId == _job.Id ? _job.Title : null);

    public Task<int> CountOpenJobsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(1);

    public Task<IReadOnlyDictionary<Guid, JobSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, JobSummary>>(new Dictionary<Guid, JobSummary>());

    public Task<JobRequirements?> GetJobRequirementsAsync(
        Guid tenantId, Guid jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult<JobRequirements?>(null);
}

internal sealed class StubCandidateAccountReader : ICandidateAccountReader
{
    private readonly CandidateAccountSummary _account;

    public StubCandidateAccountReader(CandidateAccountSummary account) => _account = account;

    public Task<CandidateAccountSummary?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<CandidateAccountSummary?>(id == _account.Id ? _account : null);
}

// Records keys instead of talking to MinIO. Tests must never reach real object storage, and "was
// anything uploaded" is exactly what the gate ordering needs to be observable.
internal sealed class RecordingFileStorage : IFileStorage
{
    public List<string> Uploaded { get; } = [];
    public List<string> Deleted { get; } = [];

    public Task UploadAsync(
        string key, Stream content, long size, string contentType,
        CancellationToken cancellationToken = default)
    {
        Uploaded.Add(key);
        return Task.CompletedTask;
    }

    public Task<string> GetPresignedDownloadUrlAsync(
        string key, TimeSpan expiry, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://storage.test/{key}");

    public Task<byte[]> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(Array.Empty<byte>());

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Deleted.Add(key);
        return Task.CompletedTask;
    }
}
