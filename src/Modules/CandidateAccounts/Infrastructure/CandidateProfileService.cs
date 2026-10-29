using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateProfileService : ICandidateProfileService
{
    private static readonly TimeSpan CvDownloadUrlExpiry = TimeSpan.FromMinutes(5);

    private readonly CandidateAccountsDbContext _db;
    private readonly ICandidatePasswordHasher _passwordHasher;
    private readonly ICandidateSessionIssuer _sessions;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly IFileStorage _fileStorage;
    private readonly CandidateEmailChangeOptions _emailChangeOptions;
    private readonly ILogger<CandidateProfileService> _logger;

    public CandidateProfileService(
        CandidateAccountsDbContext db,
        ICandidatePasswordHasher passwordHasher,
        ICandidateSessionIssuer sessions,
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        IFileStorage fileStorage,
        IOptions<CandidateEmailChangeOptions> emailChangeOptions,
        ILogger<CandidateProfileService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _emailSender = emailSender;
        _emailText = emailText;
        _fileStorage = fileStorage;
        _emailChangeOptions = emailChangeOptions.Value;
        _logger = logger;
    }

    public async Task<Result<CandidateProfileDto>> GetAsync(Guid candidateAccountId)
    {
        var account = await _db.CandidateAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidateAccountId);

        return account is null
            ? Result.Failure<CandidateProfileDto>(CandidateProfileErrors.NotFound)
            : Result.Success(ToDto(account));
    }

    public async Task<Result<CandidateProfileDto>> UpdateAsync(
        Guid candidateAccountId, UpdateCandidateProfileCommand command)
    {
        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure<CandidateProfileDto>(CandidateProfileErrors.NotFound);

        var country = NullIfWhiteSpace(command.Country);
        var city = NullIfWhiteSpace(command.City);

        if (country is not null &&
            (!SupportedCountries.CitiesByCountry.TryGetValue(country, out var cities) ||
             city is null || !cities.Contains(city)))
        {
            return Result.Failure<CandidateProfileDto>(CandidateProfileErrors.UnsupportedLocation);
        }

        try
        {
            account.UpdateProfile(
                command.FirstName, command.LastName, command.PhoneNumber, country, city, command.BirthDate);
        }
        catch (ArgumentException invariantViolation)
        {
            // Domain guards throw; turn them into a 400 instead of a 500.
            return Result.Failure<CandidateProfileDto>(
                CandidateProfileErrors.InvalidData(invariantViolation.Message));
        }

        await _db.SaveChangesAsync();
        return Result.Success(ToDto(account));
    }

    public async Task<Result> SetPreferredLanguageAsync(Guid candidateAccountId, string language)
    {
        // Rejected, not normalized: this endpoint only sets the language, so a bad value is a client bug.
        if (!SupportedLanguages.IsSupported(language))
            return Result.Failure(CandidateProfileErrors.UnsupportedLanguage);

        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure(CandidateProfileErrors.NotFound);

        account.SetPreferredLanguage(language);
        await _db.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<CandidatePasswordChangeResult>> ChangePasswordAsync(
        Guid candidateAccountId, ChangeCandidatePasswordCommand command)
    {
        if (!CandidatePasswordPolicy.IsAcceptable(command.NewPassword))
            return Result.Failure<CandidatePasswordChangeResult>(CandidateProfileErrors.PasswordTooShort);

        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure<CandidatePasswordChangeResult>(CandidateProfileErrors.NotFound);

        // A stolen session alone must not be able to lock the owner out.
        if (!_passwordHasher.Verify(account.PasswordHash, command.CurrentPassword))
            return Result.Failure<CandidatePasswordChangeResult>(CandidateProfileErrors.InvalidCurrentPassword);

        account.ChangePassword(_passwordHasher.Hash(command.NewPassword));
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Password changed for candidate account {CandidateAccountId}", candidateAccountId);

        await NotifyPasswordChangedAsync(account.Email, account.PreferredLanguage);

        // The stamp rotation also killed this session, so issue a new one for the caller.
        var session = await _sessions.IssueAsync(account);
        return Result.Success(new CandidatePasswordChangeResult(session.AccessToken, session.RefreshToken));
    }

    public async Task<Result> RequestEmailChangeAsync(
        Guid candidateAccountId, RequestCandidateEmailChangeCommand command)
    {
        if (!MailAddress.TryCreate(command.NewEmail?.Trim(), out _))
            return Result.Failure(CandidateProfileErrors.InvalidEmail);

        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure(CandidateProfileErrors.NotFound);

        // A stolen session alone must not be able to move the login to another mailbox.
        if (!_passwordHasher.Verify(account.PasswordHash, command.CurrentPassword))
            return Result.Failure(CandidateProfileErrors.InvalidCurrentPassword);

        var newEmail = CandidateAccount.NormalizeEmail(command.NewEmail!);
        if (newEmail == account.Email)
            return Result.Failure(CandidateProfileErrors.EmailUnchanged);

        // Early, friendly check only. It is repeated at confirm time.
        var emailTaken = await _db.CandidateAccounts.AnyAsync(c => c.Email == newEmail);
        if (emailTaken)
            return Result.Failure(CandidateProfileErrors.EmailAlreadyRegistered);

        // Only the latest link should work.
        var pending = await _db.EmailChangeRequests
            .Where(r => r.CandidateAccountId == account.Id && r.ConsumedAtUtc == null)
            .ToListAsync();
        _db.EmailChangeRequests.RemoveRange(pending);

        var rawToken = GenerateToken();
        _db.EmailChangeRequests.Add(EmailChangeRequest.Create(account.Id, newEmail, Hash(rawToken)));
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Email change requested for candidate account {CandidateAccountId}", candidateAccountId);

        // Not best-effort: without this mail the flow can't complete, so let a failure surface.
        await SendConfirmationLinkAsync(newEmail, account.PreferredLanguage, rawToken);

        return Result.Success();
    }

    public async Task<Result> ConfirmEmailChangeAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(CandidateProfileErrors.InvalidEmailChangeToken);

        // Hashed outside the query; EF can't translate the method call.
        var tokenHash = Hash(token);
        var request = await _db.EmailChangeRequests
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash);

        if (request is null || !request.IsValid)
            return Result.Failure(CandidateProfileErrors.InvalidEmailChangeToken);

        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == request.CandidateAccountId);
        if (account is null)
            return Result.Failure(CandidateProfileErrors.InvalidEmailChangeToken);

        // Someone may have registered this address since the request was made.
        var emailTaken = await _db.CandidateAccounts.AnyAsync(c => c.Email == request.NewEmail);
        if (emailTaken)
            return Result.Failure(CandidateProfileErrors.EmailAlreadyRegistered);

        var oldEmail = account.Email;
        account.ChangeEmail(request.NewEmail);
        request.MarkConsumed();

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A registration slipped in between the check and the save.
            return Result.Failure(CandidateProfileErrors.EmailAlreadyRegistered);
        }

        _logger.LogInformation(
            "Email changed for candidate account {CandidateAccountId}", account.Id);

        // Tell the old address, in case the owner didn't make this change.
        await NotifyEmailChangedAsync(oldEmail, account.PreferredLanguage);

        return Result.Success();
    }

    public async Task<Result<CandidateCvDto>> UploadCvAsync(
        Guid candidateAccountId, UploadCandidateCvCommand command)
    {
        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure<CandidateCvDto>(CandidateProfileErrors.NotFound);

        var fileName = StorageKey.SanitizeFileName(command.FileName);

        // No tenant in the path: this CV belongs to the person. A new guid per upload keeps the old
        // object intact if the save below fails.
        var fileKey = $"{CvKeyPrefix}/{account.Id}/{Guid.NewGuid()}-{fileName}";

        await _fileStorage.UploadAsync(
            fileKey, command.Content, command.SizeBytes, command.ContentType);

        var replacedKey = account.AttachCv(fileKey, fileName);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Don't leave an unreferenced CV (personal data) in storage.
            await TryDeleteAsync(fileKey);
            throw;
        }

        // After the save, so a failed save doesn't lose the current CV.
        if (replacedKey is not null)
            await TryDeleteAsync(replacedKey);

        _logger.LogInformation("CV attached to candidate account {CandidateAccountId}", candidateAccountId);

        return Result.Success(new CandidateCvDto(fileName, account.CvUploadedAtUtc!.Value));
    }

    public async Task<Result> RemoveCvAsync(Guid candidateAccountId)
    {
        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure(CandidateProfileErrors.NotFound);

        var removedKey = account.RemoveCv();
        if (removedKey is null)
            return Result.Success();

        await _db.SaveChangesAsync();

        // Best-effort: the CV is already unreachable once the row is saved.
        await TryDeleteAsync(removedKey);

        _logger.LogInformation("CV removed from candidate account {CandidateAccountId}", candidateAccountId);

        return Result.Success();
    }

    public async Task<Result<CandidateCvDownloadDto>> GetCvDownloadUrlAsync(Guid candidateAccountId)
    {
        var cvFileKey = await _db.CandidateAccounts
            .AsNoTracking()
            .Where(c => c.Id == candidateAccountId)
            .Select(c => c.CvFileKey)
            .FirstOrDefaultAsync();

        if (cvFileKey is null)
            return Result.Failure<CandidateCvDownloadDto>(CandidateProfileErrors.CvNotFound);

        var url = await _fileStorage.GetPresignedDownloadUrlAsync(cvFileKey, CvDownloadUrlExpiry);

        return Result.Success(
            new CandidateCvDownloadDto(url, (int)CvDownloadUrlExpiry.TotalSeconds));
    }

    // Keeps account CVs apart from the per-tenant application copies.
    private const string CvKeyPrefix = "candidates";

    private async Task TryDeleteAsync(string fileKey)
    {
        try
        {
            await _fileStorage.DeleteAsync(fileKey);
        }
        catch (Exception exception)
        {
            // Logged only; the caller's result doesn't depend on this.
            _logger.LogError(exception, "Failed to delete the CV object {CvFileKey}", fileKey);
        }
    }

    private async Task SendConfirmationLinkAsync(string newEmail, string language, string rawToken)
    {
        var link = $"{_emailChangeOptions.ConfirmBaseUrl}?token={rawToken}";
        var body = _emailText.Get(EmailTextKeys.Candidate.EmailChangeConfirmBody, language, link);

        await _emailSender.SendAsync(
            newEmail, _emailText.Get(EmailTextKeys.Candidate.EmailChangeConfirmSubject, language), body);
    }

    // Best-effort: the change is already saved.
    private async Task NotifyEmailChangedAsync(string oldEmail, string language)
    {
        var subject = _emailText.Get(EmailTextKeys.Candidate.EmailChangedSubject, language);
        var body = _emailText.Get(EmailTextKeys.Candidate.EmailChangedBody, language);

        try
        {
            await _emailSender.SendAsync(oldEmail, subject, body);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the email-changed notification email");
        }
    }

    // Also in Tenants.InvitationService. Kept per module rather than shared.
    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    // Best-effort: the change is already saved.
    private async Task NotifyPasswordChangedAsync(string email, string language)
    {
        var subject = _emailText.Get(EmailTextKeys.Candidate.PasswordChangedSubject, language);
        var body = _emailText.Get(EmailTextKeys.Candidate.PasswordChangedBody, language);

        try
        {
            await _emailSender.SendAsync(email, subject, body);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the password-changed notification email");
        }
    }

    private static CandidateProfileDto ToDto(CandidateAccount account) =>
        new(account.Id, account.Email, account.FirstName, account.LastName,
            account.PhoneNumber, account.Country, account.City, account.BirthDate,
            ToCvDto(account));

    private static CandidateCvDto? ToCvDto(CandidateAccount account) =>
        account is { CvFileName: { } fileName, CvUploadedAtUtc: { } uploadedAt }
            ? new CandidateCvDto(fileName, uploadedAt)
            : null;

    private static string? NullIfWhiteSpace(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
