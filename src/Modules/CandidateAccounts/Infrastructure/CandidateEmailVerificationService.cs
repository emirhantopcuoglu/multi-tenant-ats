using System.Net;
using System.Security.Cryptography;
using System.Text;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateEmailVerificationService : ICandidateEmailVerificationService
{
    private readonly CandidateAccountsDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTextProvider _emailText;
    private readonly CandidateEmailVerificationOptions _options;
    private readonly ILogger<CandidateEmailVerificationService> _logger;

    public CandidateEmailVerificationService(
        CandidateAccountsDbContext db,
        IEmailSender emailSender,
        IEmailTextProvider emailText,
        IOptions<CandidateEmailVerificationOptions> options,
        ILogger<CandidateEmailVerificationService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _emailText = emailText;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> SendAsync(Guid candidateAccountId, CancellationToken ct = default)
    {
        var account = await _db.CandidateAccounts
            .FirstOrDefaultAsync(c => c.Id == candidateAccountId, ct);

        if (account is null)
            return Result.Failure(CandidateEmailVerificationErrors.InvalidToken);

        if (account.IsEmailVerified)
            return Result.Failure(CandidateEmailVerificationErrors.AlreadyVerified);

        // Only the latest link should work.
        var pending = await _db.EmailVerificationRequests
            .Where(r => r.CandidateAccountId == account.Id && r.ConsumedAtUtc == null)
            .ToListAsync(ct);
        _db.EmailVerificationRequests.RemoveRange(pending);

        var rawToken = GenerateToken();
        _db.EmailVerificationRequests.Add(
            EmailVerificationRequest.Create(account.Id, Hash(rawToken)));
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Email verification link issued for candidate account {CandidateAccountId}", account.Id);

        // Best-effort: the candidate can ask for another link.
        await SendVerificationLinkAsync(account, rawToken, ct);

        return Result.Success();
    }

    public async Task<Result> ConfirmAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(CandidateEmailVerificationErrors.InvalidToken);

        // Hashed outside the query; EF can't translate the method call.
        var tokenHash = Hash(token);
        var request = await _db.EmailVerificationRequests
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

        if (request is null || !request.IsValid)
            return Result.Failure(CandidateEmailVerificationErrors.InvalidToken);

        var account = await _db.CandidateAccounts
            .FirstOrDefaultAsync(c => c.Id == request.CandidateAccountId, ct);
        if (account is null)
            return Result.Failure(CandidateEmailVerificationErrors.InvalidToken);

        var justVerified = account.MarkEmailVerified();
        request.MarkConsumed();
        await _db.SaveChangesAsync(ct);

        // No stamp rotation: credentials didn't change, so there is no reason to end sessions.
        _logger.LogInformation(
            "Email verified for candidate account {CandidateAccountId} (newly verified: {JustVerified})",
            account.Id, justVerified);

        return Result.Success();
    }

    private async Task SendVerificationLinkAsync(
        CandidateAccount account, string rawToken, CancellationToken ct)
    {
        var link = $"{_options.ConfirmBaseUrl}?token={Uri.EscapeDataString(rawToken)}";
        var language = account.PreferredLanguage;

        // The name is user input going into HTML.
        var body = _emailText.Get(
            EmailTextKeys.Candidate.VerifyEmailBody,
            language,
            WebUtility.HtmlEncode(account.FirstName),
            link,
            EmailVerificationRequest.ValidHours);

        try
        {
            await _emailSender.SendAsync(
                account.Email,
                _emailText.Get(EmailTextKeys.Candidate.VerifyEmailSubject, language),
                body,
                ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the email verification link");
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
