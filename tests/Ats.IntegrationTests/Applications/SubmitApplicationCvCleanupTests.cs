using System.Text;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Ats.IntegrationTests.Applications;

// The CV is uploaded before the application is saved, so a failed save has to delete it again.
[Collection("Integration")]
public sealed class SubmitApplicationCvCleanupTests
{
    private readonly PostgresContainerFixture _fixture;
    private readonly FixedTenant _tenant = new(Guid.NewGuid());
    private readonly PublishedJob _job = new(Guid.NewGuid(), "Staff Engineer", "staff-engineer");
    private readonly Guid _accountId = Guid.NewGuid();

    public SubmitApplicationCvCleanupTests(PostgresContainerFixture fixture) => _fixture = fixture;

    // The candidate closes the tab while the application is being saved.
    [Fact]
    public async Task the_uploaded_cv_should_be_deleted_even_when_the_request_is_cancelled()
    {
        using var request = new CancellationTokenSource();
        var storage = new RecordingFileStorage();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SubmitAsync(storage, new CancelOnSave(request), new CapturingLogger<SubmitApplicationHandler>(), request.Token));

        Assert.Single(storage.Uploaded);
        Assert.Equal(storage.Uploaded, storage.Deleted);
    }

    [Fact]
    public async Task a_cv_that_cannot_be_deleted_should_be_logged_with_its_key()
    {
        var storage = new RecordingFileStorage { DeleteFailure = new IOException("storage unavailable") };
        var logger = new CapturingLogger<SubmitApplicationHandler>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SubmitAsync(storage, new FailOnSave(), logger, CancellationToken.None));

        var key = Assert.Single(storage.Uploaded);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(key));
    }

    private async Task SubmitAsync(
        RecordingFileStorage storage, IInterceptor failure, ILogger<SubmitApplicationHandler> logger, CancellationToken ct)
    {
        await using var db = new ApplicationsDbContext(
            new DbContextOptionsBuilder<ApplicationsDbContext>(
                    PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, _tenant))
                .AddInterceptors(failure)
                .Options,
            _tenant);

        var email = $"{Guid.NewGuid():N}@acme.test";
        var handler = new SubmitApplicationHandler(
            db,
            new StubPublishedJobDirectory(_job),
            new StubCandidateAccountReader(new CandidateAccountSummary(
                _accountId, email, "Ada", "Applicant", IsEmailVerified: true, IsActive: true,
                CvFileKey: null, CvFileName: null)),
            storage,
            _tenant,
            new CapturingPublisher(),
            new InMemoryActivityLog([]),
            logger);

        var content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 pretend cv"));
        await handler.Handle(
            new SubmitApplicationCommand(
                _job.Slug, _accountId, null, null, null,
                new CvUpload(content, content.Length, "application/pdf", "cv.pdf")),
            ct);
    }

    private sealed class CancelOnSave(CancellationTokenSource request) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            request.Cancel();
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FailOnSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database unavailable");
    }
}
