using System.Text.RegularExpressions;
using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.IntegrationTests.CandidateAccounts;

// A failed login writes the same row as the candidate's own saves. Each test lands one between the
// service's read and its save.
[Collection("Integration")]
public sealed class CandidateAccountConflictRetryTests : IAsyncLifetime
{
    private const string Email = "jane@example.com";
    private const string Password = "correct horse battery";
    private const string NewPassword = "another horse battery";

    private readonly PostgresContainerFixture _fixture;

    public CandidateAccountConflictRetryTests(PostgresContainerFixture fixture)
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
    public async Task profile_update_should_save_when_a_failed_login_lands_before_it()
    {
        var accountId = await SeedAccountAsync();

        var result = await ProfileService(LoginBeforeSave(accountId)).UpdateAsync(
            accountId, new UpdateCandidateProfileCommand("Janet", "Roe", null, null, null, null));

        Assert.True(result.IsSuccess);
        var account = await LoadAsync(accountId);
        Assert.Equal("Janet", account.FirstName);
        Assert.Equal(1, account.FailedLoginCount);
    }

    [Fact]
    public async Task language_change_should_save_when_a_failed_login_lands_before_it()
    {
        var accountId = await SeedAccountAsync();

        var result = await ProfileService(LoginBeforeSave(accountId))
            .SetPreferredLanguageAsync(accountId, SupportedLanguages.Turkish);

        Assert.True(result.IsSuccess);
        Assert.Equal(SupportedLanguages.Turkish, (await LoadAsync(accountId)).PreferredLanguage);
    }

    [Fact]
    public async Task password_change_should_save_once_when_a_failed_login_lands_before_it()
    {
        var accountId = await SeedAccountAsync();
        var mail = new RecordingEmailSender();

        var result = await ProfileService(LoginBeforeSave(accountId), mail).ChangePasswordAsync(
            accountId, new ChangeCandidatePasswordCommand(Password, NewPassword));

        Assert.True(result.IsSuccess);
        Assert.True(Hasher.Verify((await LoadAsync(accountId)).PasswordHash, NewPassword));
        Assert.Single(mail.Sent);
    }

    [Fact]
    public async Task email_change_confirm_should_save_when_a_failed_login_lands_before_it()
    {
        var accountId = await SeedAccountAsync();
        var requestMail = new RecordingEmailSender();
        await ProfileService(CreateDbContext(), requestMail).RequestEmailChangeAsync(
            accountId, new RequestCandidateEmailChangeCommand("janet@example.com", Password));
        var confirmMail = new RecordingEmailSender();

        var result = await ProfileService(LoginBeforeSave(accountId), confirmMail)
            .ConfirmEmailChangeAsync(TokenFrom(requestMail));

        Assert.True(result.IsSuccess);
        Assert.Equal("janet@example.com", (await LoadAsync(accountId)).Email);
        Assert.Equal(Email, Assert.Single(confirmMail.Sent).ToEmail);
    }

    [Fact]
    public async Task cv_upload_should_attach_the_file_once_when_a_failed_login_lands_before_it()
    {
        var accountId = await SeedAccountAsync();
        var storage = new RecordingFileStorage();

        var result = await ProfileService(LoginBeforeSave(accountId), storage: storage)
            .UploadCvAsync(accountId, CvUpload());

        Assert.True(result.IsSuccess);
        var uploadedKey = Assert.Single(storage.Uploaded);
        Assert.Equal(uploadedKey, (await LoadAsync(accountId)).CvFileKey);
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task cv_removal_should_save_when_a_failed_login_lands_before_it()
    {
        var accountId = await SeedAccountAsync();
        var storage = new RecordingFileStorage();
        await ProfileService(CreateDbContext(), storage: storage).UploadCvAsync(accountId, CvUpload());

        var result = await ProfileService(LoginBeforeSave(accountId), storage: storage).RemoveCvAsync(accountId);

        Assert.True(result.IsSuccess);
        Assert.Null((await LoadAsync(accountId)).CvFileKey);
        Assert.Equal(storage.Uploaded, storage.Deleted);
    }

    [Fact]
    public async Task freeze_should_save_when_a_failed_login_lands_before_it()
    {
        var accountId = await SeedAccountAsync();

        var result = await LifecycleService(LoginBeforeSave(accountId)).FreezeAsync(accountId);

        Assert.True(result.IsSuccess);
        Assert.Equal(CandidateAccountStatus.Frozen, (await LoadAsync(accountId)).Status);
    }

    [Fact]
    public async Task reactivate_should_save_when_a_failed_login_lands_before_it()
    {
        var accountId = await SeedAccountAsync();
        await LifecycleService(CreateDbContext()).FreezeAsync(accountId);

        var result = await LifecycleService(LoginBeforeSave(accountId)).ReactivateAsync(accountId);

        Assert.True(result.IsSuccess);
        Assert.Equal(CandidateAccountStatus.Active, (await LoadAsync(accountId)).Status);
    }

    [Fact]
    public async Task delete_should_save_when_a_failed_login_lands_before_it()
    {
        var accountId = await SeedAccountAsync();

        var result = await LifecycleService(LoginBeforeSave(accountId))
            .DeleteAsync(accountId, new DeleteCandidateAccountCommand(Password));

        Assert.True(result.IsSuccess);
        Assert.Equal(CandidateAccountStatus.Deleted, (await LoadAsync(accountId)).Status);
    }

    [Fact]
    public async Task email_verification_should_save_when_a_failed_login_lands_before_it()
    {
        var accountId = await SeedAccountAsync();
        var mail = new RecordingEmailSender();
        await CandidateServiceFactory.EmailVerification(CreateDbContext(), mail).SendAsync(accountId);

        var result = await CandidateServiceFactory.EmailVerification(LoginBeforeSave(accountId))
            .ConfirmAsync(TokenFrom(mail));

        Assert.True(result.IsSuccess);
        Assert.True((await LoadAsync(accountId)).IsEmailVerified);
    }

    [Fact]
    public async Task password_reset_should_save_when_a_failed_login_lands_before_it()
    {
        var accountId = await SeedAccountAsync();
        var mail = new RecordingEmailSender();
        await ResetService(CreateDbContext(), mail).RequestAsync(Email);

        var result = await ResetService(LoginBeforeSave(accountId), new RecordingEmailSender())
            .ResetAsync(TokenFrom(mail), NewPassword);

        Assert.True(result.IsSuccess);
        Assert.True(Hasher.Verify((await LoadAsync(accountId)).PasswordHash, NewPassword));
    }

    // The retry has to decide again on the fresh row, not push the old decision through.
    [Fact]
    public async Task reactivate_should_not_bring_back_an_account_deleted_before_its_save()
    {
        var accountId = await SeedAccountAsync();
        await LifecycleService(CreateDbContext()).FreezeAsync(accountId);
        var db = CreateDbContext(new WriteBeforeFirstSave(() => DeleteAccountAsync(accountId)));

        var result = await LifecycleService(db).ReactivateAsync(accountId);

        Assert.True(result.IsFailure);
        Assert.Equal(CandidateAccountStatus.Deleted, (await LoadAsync(accountId)).Status);
    }

    private static readonly CandidatePasswordHasher Hasher = new(new PasswordHasher<CandidateAccount>());

    private static IOptions<CandidateJwtOptions> JwtOptions =>
        Options.Create(new CandidateJwtOptions
        {
            Secret = "candidate-conflict-retry-tests-signing-key",
            Issuer = "ats-tests",
            Audience = "ats-tests",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

    private CandidateAccountsDbContext LoginBeforeSave(Guid accountId) =>
        CreateDbContext(new WriteBeforeFirstSave(() => RecordFailedLoginAsync(accountId)));

    private async Task RecordFailedLoginAsync(Guid accountId)
    {
        await using var db = CreateDbContext();
        var account = await db.CandidateAccounts.SingleAsync(c => c.Id == accountId);
        account.RegisterFailedLogin(5, TimeSpan.FromMinutes(15), DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task DeleteAccountAsync(Guid accountId)
    {
        await using var db = CreateDbContext();
        var account = await db.CandidateAccounts.SingleAsync(c => c.Id == accountId);
        account.Delete();
        await db.SaveChangesAsync();
    }

    private static UploadCandidateCvCommand CvUpload() =>
        new(new MemoryStream([1, 2, 3]), 3, "application/pdf", "cv.pdf");

    private static string TokenFrom(RecordingEmailSender mail)
    {
        var match = Regex.Match(mail.Sent[^1].Body, @"[?&]token=([^""&\s<]+)");
        Assert.True(match.Success, "The mail did not contain a token link.");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private CandidateProfileService ProfileService(
        CandidateAccountsDbContext db, RecordingEmailSender? mail = null, RecordingFileStorage? storage = null) =>
        new(db,
            Hasher,
            new CandidateSessionIssuer(db, new CandidateTokenService(JwtOptions), JwtOptions),
            mail ?? new RecordingEmailSender(),
            new JsonEmailTextProvider(),
            storage ?? new RecordingFileStorage(),
            Options.Create(new CandidateEmailChangeOptions()),
            NullLogger<CandidateProfileService>.Instance);

    private static CandidateAccountLifecycleService LifecycleService(CandidateAccountsDbContext db) =>
        new(db, Hasher, new RecordingFileStorage(), NullLogger<CandidateAccountLifecycleService>.Instance);

    private static CandidatePasswordResetService ResetService(
        CandidateAccountsDbContext db, RecordingEmailSender mail) =>
        new(db,
            Hasher,
            mail,
            new JsonEmailTextProvider(),
            Options.Create(new CandidatePasswordResetOptions { ResetBaseUrl = "https://ats.test/reset" }),
            NullLogger<CandidatePasswordResetService>.Instance);

    private async Task<Guid> SeedAccountAsync()
    {
        await using var db = CreateDbContext();
        var account = CandidateAccount.Register(
            Email, Hasher.Hash(Password), "Jane", "Doe", SupportedLanguages.Default);
        db.CandidateAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private async Task<CandidateAccount> LoadAsync(Guid accountId)
    {
        await using var db = CreateDbContext();
        return await db.CandidateAccounts.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == accountId);
    }

    private CandidateAccountsDbContext CreateDbContext(IInterceptor? beforeSave = null)
    {
        var options = PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString);
        return beforeSave is null
            ? new CandidateAccountsDbContext(options)
            : new CandidateAccountsDbContext(
                new DbContextOptionsBuilder<CandidateAccountsDbContext>(options).AddInterceptors(beforeSave).Options);
    }

    private sealed class WriteBeforeFirstSave : SaveChangesInterceptor
    {
        private readonly Func<Task> _competingWrite;
        private bool _hasRun;

        public WriteBeforeFirstSave(Func<Task> competingWrite)
        {
            _competingWrite = competingWrite;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_hasRun)
            {
                _hasRun = true;
                await _competingWrite();
            }

            return result;
        }
    }
}
