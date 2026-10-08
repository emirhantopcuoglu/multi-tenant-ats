using System.Text;
using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.CandidateAccounts;

// The CV lives in two places that have to stay in step: a row in Postgres and an object in storage.
// Every test here is about the pair, because each half looks fine on its own while the other is
// wrong — a row pointing at a deleted object, or an object no row will ever name again.
[Collection("Integration")]
public sealed class CandidateProfileCvTests : IAsyncLifetime
{
    private const string Password = "correct-horse-battery";

    private readonly PostgresContainerFixture _fixture;

    public CandidateProfileCvTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM candidate_accounts.\"CandidateAccounts\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Uploading_should_store_the_object_and_record_it_on_the_account()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        var storage = new RecordingFileStorage();

        // Act
        var result = await CreateService(storage).UploadCvAsync(accountId, CvCommand("resume.pdf"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("resume.pdf", result.Value.FileName);

        var storedKey = await ReadCvFileKeyAsync(accountId);
        Assert.NotNull(storedKey);
        Assert.Equal([storedKey], storage.Uploaded);

        // Keyed under the account and under no tenant: this CV belongs to the person.
        Assert.StartsWith($"candidates/{accountId}/", storedKey);
    }

    [Fact]
    public async Task Uploading_should_sanitize_the_file_name_before_it_reaches_the_key()
    {
        // A name that tries to climb out of its prefix must not be able to address another object.
        var accountId = await SeedAccountAsync();
        var storage = new RecordingFileStorage();

        var result = await CreateService(storage).UploadCvAsync(accountId, CvCommand("../../etc/passwd.pdf"));

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("..", storage.Uploaded.Single());
        Assert.StartsWith($"candidates/{accountId}/", storage.Uploaded.Single());
    }

    [Fact]
    public async Task Replacing_should_delete_the_previous_object_and_keep_the_new_one()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        var storage = new RecordingFileStorage();
        await CreateService(storage).UploadCvAsync(accountId, CvCommand("old.pdf"));
        var firstKey = await ReadCvFileKeyAsync(accountId);

        // Act
        await CreateService(storage).UploadCvAsync(accountId, CvCommand("new.pdf"));

        // Assert — the displaced object is gone, the current one is untouched
        var secondKey = await ReadCvFileKeyAsync(accountId);
        Assert.NotEqual(firstKey, secondKey);
        Assert.Equal(firstKey, storage.Deleted.Single());
        Assert.DoesNotContain(secondKey, storage.Deleted);
        Assert.Equal("new.pdf", (await CreateService(storage).GetAsync(accountId)).Value.Cv!.FileName);
    }

    [Fact]
    public async Task Removing_should_clear_the_account_and_delete_the_object()
    {
        var accountId = await SeedAccountAsync();
        var storage = new RecordingFileStorage();
        await CreateService(storage).UploadCvAsync(accountId, CvCommand("cv.pdf"));
        var key = await ReadCvFileKeyAsync(accountId);

        var result = await CreateService(storage).RemoveCvAsync(accountId);

        Assert.True(result.IsSuccess);
        Assert.Null(await ReadCvFileKeyAsync(accountId));
        Assert.Equal(key, storage.Deleted.Single());
    }

    [Fact]
    public async Task Removing_a_cv_that_is_not_there_should_succeed_without_touching_storage()
    {
        // DELETE is idempotent; a candidate clicking twice must not see an error, and the second
        // call has no object to delete.
        var accountId = await SeedAccountAsync();
        var storage = new RecordingFileStorage();

        var result = await CreateService(storage).RemoveCvAsync(accountId);

        Assert.True(result.IsSuccess);
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Asking_for_a_download_url_without_a_cv_should_fail()
    {
        var accountId = await SeedAccountAsync();

        var result = await CreateService().GetCvDownloadUrlAsync(accountId);

        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.CvNotFound, result.Error);
    }

    [Fact]
    public async Task Asking_for_a_download_url_should_return_a_signed_link_for_the_stored_key()
    {
        var accountId = await SeedAccountAsync();
        var storage = new RecordingFileStorage();
        await CreateService(storage).UploadCvAsync(accountId, CvCommand("cv.pdf"));
        var key = await ReadCvFileKeyAsync(accountId);

        var result = await CreateService(storage).GetCvDownloadUrlAsync(accountId);

        Assert.True(result.IsSuccess);
        Assert.Contains(key!, result.Value.Url);
        Assert.True(result.Value.ExpiresInSeconds > 0);
    }

    [Fact]
    public async Task The_profile_should_report_no_cv_until_one_is_uploaded()
    {
        var accountId = await SeedAccountAsync();

        var before = await CreateService().GetAsync(accountId);
        Assert.Null(before.Value.Cv);

        var storage = new RecordingFileStorage();
        await CreateService(storage).UploadCvAsync(accountId, CvCommand("cv.pdf"));

        var after = await CreateService(storage).GetAsync(accountId);
        Assert.NotNull(after.Value.Cv);
        Assert.Equal("cv.pdf", after.Value.Cv!.FileName);
    }

    [Fact]
    public async Task Deleting_the_account_should_delete_the_cv_object_too()
    {
        // Erasure has to follow the personal data out of the database: clearing the column while
        // the file stays in the bucket is not deletion.
        var accountId = await SeedAccountAsync();
        var storage = new RecordingFileStorage();
        await CreateService(storage).UploadCvAsync(accountId, CvCommand("cv.pdf"));
        var key = await ReadCvFileKeyAsync(accountId);

        var result = await CreateLifecycleService(storage)
            .DeleteAsync(accountId, new DeleteCandidateAccountCommand(Password));

        Assert.True(result.IsSuccess);
        Assert.Equal(key, storage.Deleted.Single());
    }

    private static UploadCandidateCvCommand CvCommand(string fileName)
    {
        var content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 pretend cv"));
        return new UploadCandidateCvCommand(content, content.Length, "application/pdf", fileName);
    }

    private async Task<string?> ReadCvFileKeyAsync(Guid accountId)
    {
        await using var db = CreateDbContext();
        return await db.CandidateAccounts
            .AsNoTracking()
            .Where(c => c.Id == accountId)
            .Select(c => c.CvFileKey)
            .SingleAsync();
    }

    private async Task<Guid> SeedAccountAsync()
    {
        await using var db = CreateDbContext();
        var account = CandidateAccount.Register(
            "jane@example.com", CreatePasswordHasher().Hash(Password), "Jane", "Doe",
            SupportedLanguages.Default);

        db.CandidateAccounts.Add(account);
        await db.SaveChangesAsync();

        return account.Id;
    }

    private CandidateProfileService CreateService(RecordingFileStorage? fileStorage = null)
    {
        var db = CreateDbContext();

        return new CandidateProfileService(
            db,
            CreatePasswordHasher(),
            new CandidateSessionIssuer(db, new CandidateTokenService(JwtOptions), JwtOptions),
            new RecordingEmailSender(),
            new JsonEmailTextProvider(),
            fileStorage ?? new RecordingFileStorage(),
            Options.Create(new CandidateEmailChangeOptions()),
            NullLogger<CandidateProfileService>.Instance);
    }

    private CandidateAccountLifecycleService CreateLifecycleService(RecordingFileStorage fileStorage) =>
        new(CreateDbContext(), CreatePasswordHasher(), fileStorage,
            NullLogger<CandidateAccountLifecycleService>.Instance);

    private static CandidatePasswordHasher CreatePasswordHasher() =>
        new(new PasswordHasher<CandidateAccount>());

    private static IOptions<CandidateJwtOptions> JwtOptions =>
        Options.Create(new CandidateJwtOptions
        {
            Secret = "candidate-cv-tests-secret-key-at-least-32-bytes",
            Issuer = "ats-tests",
            Audience = "ats-tests",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

    private CandidateAccountsDbContext CreateDbContext() =>
        new(PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));
}
