using System.Text;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ats.IntegrationTests.Applications;

// The domain rule is unit-tested in CandidateContactDetailsTests; what these pin is that
// SubmitApplicationHandler actually calls it. The bug was one of omission — the handler read
// command.Phone and command.LinkedInUrl only on the branch that creates a candidate — so a test
// that never goes through the handler would not have caught it.
[Collection("Integration")]
public sealed class CandidateContactRefreshTests
{
    private const string OldPhone = "+90 555 000 0000";
    private const string OldLinkedIn = "https://linkedin.com/in/old";

    private readonly PostgresContainerFixture _fixture;

    public CandidateContactRefreshTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task A_returning_candidate_should_update_the_contact_details_on_file()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var email = $"{Guid.NewGuid():N}@acme.test";
        var candidateId = await SeedCandidateAsync(tenant, email);

        var result = await SubmitAsync(
            tenant, email, phone: "+90 555 111 2222", linkedIn: "https://linkedin.com/in/new");

        Assert.True(result.IsSuccess);

        var stored = await ReadCandidateAsync(tenant, candidateId);
        Assert.Equal("+90 555 111 2222", stored.Phone);
        Assert.Equal("https://linkedin.com/in/new", stored.LinkedInUrl);
    }

    [Fact]
    public async Task Applying_without_filling_the_optional_fields_should_keep_what_is_on_file()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var email = $"{Guid.NewGuid():N}@acme.test";
        var candidateId = await SeedCandidateAsync(tenant, email);

        var result = await SubmitAsync(tenant, email, phone: null, linkedIn: null);

        Assert.True(result.IsSuccess);

        var stored = await ReadCandidateAsync(tenant, candidateId);
        Assert.Equal(OldPhone, stored.Phone);
        Assert.Equal(OldLinkedIn, stored.LinkedInUrl);
    }

    [Fact]
    public async Task A_refused_duplicate_should_not_change_the_details_on_file()
    {
        // The refresh sits after the duplicate check on purpose: an application that was turned away
        // is not an application, and must not leave a trace on the candidate's record.
        var tenant = new FixedTenant(Guid.NewGuid());
        var email = $"{Guid.NewGuid():N}@acme.test";
        var candidateId = await SeedCandidateAsync(tenant, email);
        var jobId = Guid.NewGuid();

        var first = await SubmitAsync(tenant, email, phone: null, linkedIn: null, jobId: jobId);
        Assert.True(first.IsSuccess);

        var second = await SubmitAsync(
            tenant, email, phone: "+90 555 999 8888", linkedIn: null, jobId: jobId);

        Assert.True(second.IsFailure);
        Assert.Equal(ApplicationErrors.DuplicateApplication.Code, second.Error.Code);

        var stored = await ReadCandidateAsync(tenant, candidateId);
        Assert.Equal(OldPhone, stored.Phone);
    }

    private async Task<Result<Guid>> SubmitAsync(
        FixedTenant tenant, string email, string? phone, string? linkedIn, Guid? jobId = null)
    {
        var accountId = Guid.NewGuid();
        var job = new PublishedJob(jobId ?? Guid.NewGuid(), "Staff Engineer", "staff-engineer");

        await using var db = NewDb(tenant);
        var handler = new SubmitApplicationHandler(
            db,
            new StubPublishedJobDirectory(job),
            new StubCandidateAccountReader(new CandidateAccountSummary(
                accountId, email, "Ada", "Applicant", IsEmailVerified: true,
                CvFileKey: null, CvFileName: null)),
            new RecordingFileStorage(),
            tenant,
            new CapturingPublisher(),
            new InMemoryActivityLog([]),
            NullLogger<SubmitApplicationHandler>.Instance);

        var content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 pretend cv"));
        return await handler.Handle(
            new SubmitApplicationCommand(
                job.Slug, accountId, phone, linkedIn, null,
                new CvUpload(content, content.Length, "application/pdf", "cv.pdf")),
            CancellationToken.None);
    }

    private async Task<Guid> SeedCandidateAsync(FixedTenant tenant, string email)
    {
        await using var db = NewDb(tenant);
        var candidate = Candidate.Create(email, "Ada", "Applicant", OldPhone, OldLinkedIn);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();
        return candidate.Id;
    }

    private async Task<Candidate> ReadCandidateAsync(FixedTenant tenant, Guid candidateId)
    {
        await using var db = NewDb(tenant);
        return await db.Candidates.AsNoTracking().SingleAsync(c => c.Id == candidateId);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
