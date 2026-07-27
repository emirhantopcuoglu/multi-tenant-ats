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

// An application owns its CV. The candidate may attach a file, or fall back on the one saved to
// their account — but either way the document ends up as this application's own object, under this
// tenant's prefix. Pointing at the account's key instead would look identical in the database and
// break the day the candidate replaces or erases their CV, taking the recruiter's copy with it.
[Collection("Integration")]
public sealed class SubmitApplicationCvSourceTests
{
    private const string AccountCvKey = "candidates/8f2b/e3c1-account-cv.pdf";

    private readonly PostgresContainerFixture _fixture;

    public SubmitApplicationCvSourceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task An_attached_file_should_be_uploaded_under_the_tenant_prefix()
    {
        var tenantId = Guid.NewGuid();
        var storage = new RecordingFileStorage();

        var result = await SubmitAsync(tenantId, storage, WithCv(), accountCvKey: null);

        Assert.True(result.IsSuccess);
        Assert.StartsWith($"{tenantId}/", storage.Uploaded.Single());
        Assert.Empty(storage.Copied);
    }

    [Fact]
    public async Task No_attached_file_should_copy_the_account_cv_instead_of_referencing_it()
    {
        var tenantId = Guid.NewGuid();
        var storage = new RecordingFileStorage();

        var result = await SubmitAsync(tenantId, storage, cv: null, accountCvKey: AccountCvKey);

        Assert.True(result.IsSuccess);

        // Copied, not uploaded: the bytes never travel through this process.
        Assert.Empty(storage.Uploaded);
        var (source, destination) = storage.Copied.Single();
        Assert.Equal(AccountCvKey, source);

        // The destination is this tenant's object, and the stored application points at that copy —
        // not at the account key it came from.
        Assert.StartsWith($"{tenantId}/", destination);
        Assert.Equal(destination, await ReadStoredCvKeyAsync(tenantId));
    }

    [Fact]
    public async Task Neither_a_file_nor_an_account_cv_should_be_refused()
    {
        var tenantId = Guid.NewGuid();
        var storage = new RecordingFileStorage();

        var result = await SubmitAsync(tenantId, storage, cv: null, accountCvKey: null);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.CvRequired.Code, result.Error.Code);

        // Nothing was written anywhere: the refusal happens before the application row exists.
        Assert.Empty(storage.Uploaded);
        Assert.Empty(storage.Copied);
        Assert.Equal(0, await CountApplicationsAsync(tenantId));
    }

    private static CvUpload WithCv()
    {
        var content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 pretend cv"));
        return new CvUpload(content, content.Length, "application/pdf", "cv.pdf");
    }

    private async Task<Result<Guid>> SubmitAsync(
        Guid tenantId, IFileStorage storage, CvUpload? cv, string? accountCvKey)
    {
        var tenant = new FixedTenant(tenantId);
        var accountId = Guid.NewGuid();
        var job = new PublishedJob(Guid.NewGuid(), "Staff Engineer", "staff-engineer");

        await using var db = NewDb(tenant);
        var handler = new SubmitApplicationHandler(
            db,
            new StubPublishedJobDirectory(job),
            new StubCandidateAccountReader(new CandidateAccountSummary(
                accountId, $"{Guid.NewGuid():N}@acme.test", "Test", "Candidate", IsEmailVerified: true,
                CvFileKey: accountCvKey,
                CvFileName: accountCvKey is null ? null : "account-cv.pdf")),
            storage,
            tenant,
            new CapturingPublisher(),
            new InMemoryActivityLog([]),
            NullLogger<SubmitApplicationHandler>.Instance);

        return await handler.Handle(
            new SubmitApplicationCommand(
                JobSlug: job.Slug,
                CandidateAccountId: accountId,
                Phone: null,
                LinkedInUrl: null,
                CoverLetter: null,
                Cv: cv),
            CancellationToken.None);
    }

    private async Task<string?> ReadStoredCvKeyAsync(Guid tenantId)
    {
        await using var db = NewDb(new FixedTenant(tenantId));
        return await db.Applications.AsNoTracking().Select(a => a.CvFileKey).SingleAsync();
    }

    private async Task<int> CountApplicationsAsync(Guid tenantId)
    {
        await using var db = NewDb(new FixedTenant(tenantId));
        return await db.Applications.CountAsync();
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
