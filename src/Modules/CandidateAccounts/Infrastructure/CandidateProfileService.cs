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
    // The bucket is private, so a CV can only be reached through a signed, expiring link. Five
    // minutes matches the recruiter-side download: long enough to click, short enough that a leaked
    // URL is worth little.
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

        // Catalogue membership is checked here, not in the domain: the entity owns self-contained
        // invariants, the boundary owns "is this value in the supported list" (same split as Jobs).
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
            // Domain guards throw; over HTTP that must read as a 400 with the failed rule, not a 500.
            return Result.Failure<CandidateProfileDto>(
                CandidateProfileErrors.InvalidData(invariantViolation.Message));
        }

        await _db.SaveChangesAsync();
        return Result.Success(ToDto(account));
    }

    public async Task<Result> SetPreferredLanguageAsync(Guid candidateAccountId, string language)
    {
        // Rejected rather than normalized: unlike registration, where the language rides along with a
        // request that is really about something else, this endpoint exists only to set it — a value
        // outside the catalogue here is a client bug and quietly storing English would hide it.
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

        // A valid token is not enough to change the password: a stolen session must not be able to
        // lock the real owner out, so the current password re-proves ownership here.
        if (!_passwordHasher.Verify(account.PasswordHash, command.CurrentPassword))
            return Result.Failure<CandidatePasswordChangeResult>(CandidateProfileErrors.InvalidCurrentPassword);

        account.ChangePassword(_passwordHasher.Hash(command.NewPassword));
        await _db.SaveChangesAsync();

        // Structured security-event log: the id is enough to investigate, and neither password nor
        // hash may ever appear in a log line.
        _logger.LogInformation(
            "Password changed for candidate account {CandidateAccountId}", candidateAccountId);

        await NotifyPasswordChangedAsync(account.Email, account.PreferredLanguage);

        // The rotation above just invalidated this request's access token AND every refresh token
        // issued under the old stamp; issue a fresh pair so the candidate's own session survives
        // their password change while all the others stay revoked.
        var session = await _sessions.IssueAsync(account);
        return Result.Success(new CandidatePasswordChangeResult(session.AccessToken, session.RefreshToken));
    }

    public async Task<Result> RequestEmailChangeAsync(
        Guid candidateAccountId, RequestCandidateEmailChangeCommand command)
    {
        // Format check at the boundary: this string becomes the login identity, so "not obviously an
        // email" must be rejected before it is ever persisted or mailed to.
        if (!MailAddress.TryCreate(command.NewEmail?.Trim(), out _))
            return Result.Failure(CandidateProfileErrors.InvalidEmail);

        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure(CandidateProfileErrors.NotFound);

        // Same ownership re-proof as the password change: a stolen token must not be enough to point
        // the login identity at an attacker's mailbox.
        if (!_passwordHasher.Verify(account.PasswordHash, command.CurrentPassword))
            return Result.Failure(CandidateProfileErrors.InvalidCurrentPassword);

        var newEmail = CandidateAccount.NormalizeEmail(command.NewEmail!);
        if (newEmail == account.Email)
            return Result.Failure(CandidateProfileErrors.EmailUnchanged);

        // Pre-check for a clear error now; the unique index re-checked at confirm time remains the
        // real guard, because this can go stale during the one-hour window.
        var emailTaken = await _db.CandidateAccounts.AnyAsync(c => c.Email == newEmail);
        if (emailTaken)
            return Result.Failure(CandidateProfileErrors.EmailAlreadyRegistered);

        // A newer request supersedes older pending ones — only the latest mailed link may work, so a
        // forgotten link to a previously typo'd address cannot resurface later. Loaded and removed
        // (not bulk-deleted): an account has at most a handful of pending rows.
        var pending = await _db.EmailChangeRequests
            .Where(r => r.CandidateAccountId == account.Id && r.ConsumedAtUtc == null)
            .ToListAsync();
        _db.EmailChangeRequests.RemoveRange(pending);

        var rawToken = GenerateToken();
        _db.EmailChangeRequests.Add(EmailChangeRequest.Create(account.Id, newEmail, Hash(rawToken)));
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Email change requested for candidate account {CandidateAccountId}", candidateAccountId);

        // NOT best-effort like the notifications below: this mail IS the flow — if it cannot be
        // sent, the request must fail loudly so the candidate retries instead of waiting for a link
        // that will never arrive. (A retry supersedes the row just written.)
        await SendConfirmationLinkAsync(newEmail, account.PreferredLanguage, rawToken);

        return Result.Success();
    }

    public async Task<Result> ConfirmEmailChangeAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(CandidateProfileErrors.InvalidEmailChangeToken);

        // Hashed outside the query: EF can translate a comparison against a variable, not a call to
        // a local static method.
        var tokenHash = Hash(token);
        var request = await _db.EmailChangeRequests
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash);

        // Unknown, expired and consumed all collapse into one answer on purpose — the error already
        // explains why to the reader of CandidateProfileErrors.
        if (request is null || !request.IsValid)
            return Result.Failure(CandidateProfileErrors.InvalidEmailChangeToken);

        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == request.CandidateAccountId);
        if (account is null)
            return Result.Failure(CandidateProfileErrors.InvalidEmailChangeToken);

        // The one-hour window is long enough for someone else to register this address; without the
        // re-check the unique index would turn that race into a raw 500.
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
            // Concurrent registration slipped between the pre-check and the commit; the unique index
            // rejected it. Same friendly error as the pre-check.
            return Result.Failure(CandidateProfileErrors.EmailAlreadyRegistered);
        }

        _logger.LogInformation(
            "Email changed for candidate account {CandidateAccountId}", account.Id);

        // Hijack tripwire: the OLD mailbox is told after the fact. If the owner didn't do this,
        // that notification is their only signal before the attacker owns the login.
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

        // Keyed under the account, with no tenant in the path: this CV belongs to the person, not to
        // any company. A fresh guid per upload means a replacement never overwrites the object the
        // old row still points at, so a failed commit below leaves the previous CV intact.
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
            // The upload is not part of the database transaction. If the row cannot be written, the
            // object just stored is unreferenced — and an unreferenced CV is a PII leak, not just
            // wasted space.
            await TryDeleteAsync(fileKey);
            throw;
        }

        // Only after the commit: deleting first would destroy the live CV if the write then failed.
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

        // Best-effort and after the commit, in that order: the candidate asked for the CV to be
        // gone, and it is gone from every read path the moment the row is written. A failed object
        // delete leaves a file nobody can reach through the app, which is worth a log line and a
        // later sweep — not a failed request that tells them the removal did not work.
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

        // One answer for "no such account" and "no CV on it": the caller is asking for their own
        // account, so the distinction carries no information they do not already have.
        if (cvFileKey is null)
            return Result.Failure<CandidateCvDownloadDto>(CandidateProfileErrors.CvNotFound);

        var url = await _fileStorage.GetPresignedDownloadUrlAsync(cvFileKey, CvDownloadUrlExpiry);

        return Result.Success(
            new CandidateCvDownloadDto(url, (int)CvDownloadUrlExpiry.TotalSeconds));
    }

    // Groups every account-owned CV under one prefix, so storage lifecycle rules (and a human
    // reading the bucket) can tell them apart from the per-tenant application copies.
    private const string CvKeyPrefix = "candidates";

    private async Task TryDeleteAsync(string fileKey)
    {
        try
        {
            await _fileStorage.DeleteAsync(fileKey);
        }
        catch (Exception exception)
        {
            // Swallowed on purpose, but never silently: the caller's operation has already
            // succeeded (or is already failing for a better reason), and this leaves an orphan that
            // only the log can point at.
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

    // Best-effort: the change is already committed, so a failing mail server must not turn a
    // succeeded operation into an error — but the old owner should still hear about it if possible.
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

    // Same construction as Tenants.InvitationService: 256 bits of randomness, URL-safe base64. Ten
    // lines duplicated across modules on purpose — hoisting them into the shared kernel would couple
    // two modules over an implementation detail that is not a contract.
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

    // Best-effort by design: the password change is already committed, so a failing mail server must
    // not turn a succeeded operation into an error response. The failure is logged, not swallowed.
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
