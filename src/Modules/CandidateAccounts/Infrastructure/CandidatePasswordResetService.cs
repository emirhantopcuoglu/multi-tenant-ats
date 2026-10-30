using System.Security.Cryptography;
using System.Text;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidatePasswordResetService : ICandidatePasswordResetService
{
    private readonly CandidateAccountsDbContext _db;
    private readonly ICandidatePasswordHasher _passwordHasher;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly CandidatePasswordResetOptions _options;
    private readonly ILogger<CandidatePasswordResetService> _logger;

    public CandidatePasswordResetService(
        CandidateAccountsDbContext db,
        ICandidatePasswordHasher passwordHasher,
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        IOptions<CandidatePasswordResetOptions> options,
        ILogger<CandidatePasswordResetService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
        _emailText = emailText;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> RequestAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = CandidateAccount.NormalizeEmail(email ?? string.Empty);

        var account = await _db.CandidateAccounts
            .FirstOrDefaultAsync(c => c.Email == normalizedEmail, ct);

        if (account is null)
        {
            // Same answer as for a registered email.
            _logger.LogInformation("Password reset requested for an unregistered email");
            return Result.Success();
        }

        // Only the latest link should work.
        var pending = await _db.PasswordResetRequests
            .Where(r => r.CandidateAccountId == account.Id && r.ConsumedAtUtc == null)
            .ToListAsync(ct);
        _db.PasswordResetRequests.RemoveRange(pending);

        var rawToken = GenerateToken();
        _db.PasswordResetRequests.Add(PasswordResetRequest.Create(account.Id, Hash(rawToken)));
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Password reset requested for candidate account {CandidateAccountId}", account.Id);

        // Best-effort: failing here would reveal that the email is registered.
        await SendResetLinkAsync(account.Email, account.PreferredLanguage, rawToken, ct);

        return Result.Success();
    }

    public async Task<Result> ResetAsync(string token, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(CandidatePasswordResetErrors.InvalidToken);

        // Checked first so a weak password doesn't use up a valid link.
        if (!CandidatePasswordPolicy.IsAcceptable(newPassword))
            return Result.Failure(CandidatePasswordResetErrors.PasswordTooShort);

        // Hashed outside the query; EF can't translate the method call.
        var tokenHash = Hash(token);
        var request = await _db.PasswordResetRequests
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

        if (request is null || !request.IsValid)
            return Result.Failure(CandidatePasswordResetErrors.InvalidToken);

        var account = await _db.CandidateAccounts
            .FirstOrDefaultAsync(c => c.Id == request.CandidateAccountId, ct);
        if (account is null)
            return Result.Failure(CandidatePasswordResetErrors.InvalidToken);

        // Rotates the stamp, so every existing session ends.
        account.ChangePassword(_passwordHasher.Hash(newPassword));
        request.MarkConsumed();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Password reset completed for candidate account {CandidateAccountId}", account.Id);

        await NotifyPasswordResetAsync(account.Email, account.PreferredLanguage, ct);

        return Result.Success();
    }

    private async Task SendResetLinkAsync(string email, string language, string rawToken, CancellationToken ct)
    {
        var link = $"{_options.ResetBaseUrl}?token={Uri.EscapeDataString(rawToken)}";
        var body = _emailText.Get(EmailTextKeys.Candidate.ResetPasswordBody, language, link);

        try
        {
            await _emailSender.SendAsync(
                email, _emailText.Get(EmailTextKeys.Candidate.ResetPasswordSubject, language), body, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the password reset email");
        }
    }

    // Best-effort: the reset is already saved.
    private async Task NotifyPasswordResetAsync(string email, string language, CancellationToken ct)
    {
        var subject = _emailText.Get(EmailTextKeys.Candidate.PasswordResetSubject, language);
        var body = _emailText.Get(EmailTextKeys.Candidate.PasswordResetBody, language);

        try
        {
            await _emailSender.SendAsync(email, subject, body, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the password-reset notification email");
        }
    }

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
}
