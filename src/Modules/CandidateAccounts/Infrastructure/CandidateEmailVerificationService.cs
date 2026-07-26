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
    private readonly CandidateEmailVerificationOptions _options;
    private readonly ILogger<CandidateEmailVerificationService> _logger;

    public CandidateEmailVerificationService(
        CandidateAccountsDbContext db,
        IEmailSender emailSender,
        IOptions<CandidateEmailVerificationOptions> options,
        ILogger<CandidateEmailVerificationService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> SendAsync(Guid candidateAccountId, CancellationToken ct = default)
    {
        var account = await _db.CandidateAccounts
            .FirstOrDefaultAsync(c => c.Id == candidateAccountId, ct);

        // Only ever reached with the id from a valid candidate token, so a miss means the account was
        // deleted mid-session. InvalidToken rather than a "not found": there is no verifiable address.
        if (account is null)
            return Result.Failure(CandidateEmailVerificationErrors.InvalidToken);

        if (account.IsEmailVerified)
            return Result.Failure(CandidateEmailVerificationErrors.AlreadyVerified);

        // A newer link supersedes older pending ones, so an old email cannot be used to verify after
        // the address has since been changed. Loaded and removed rather than bulk-deleted: an account
        // has at most a handful of these.
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

        // Best-effort: the row is already committed, so a failing mail server must not turn a stored
        // token into an error response — the candidate can simply ask again. Logged as an error, not
        // swallowed, so an SMTP outage is visible to an operator.
        await SendVerificationLinkAsync(account.Email, account.FirstName, rawToken, ct);

        return Result.Success();
    }

    public async Task<Result> ConfirmAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(CandidateEmailVerificationErrors.InvalidToken);

        // Hashed outside the query: EF can translate a comparison against a variable, not a call to a
        // local static method.
        var tokenHash = Hash(token);
        var request = await _db.EmailVerificationRequests
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

        if (request is null || !request.IsValid)
            return Result.Failure(CandidateEmailVerificationErrors.InvalidToken);

        var account = await _db.CandidateAccounts
            .FirstOrDefaultAsync(c => c.Id == request.CandidateAccountId, ct);
        if (account is null)
            return Result.Failure(CandidateEmailVerificationErrors.InvalidToken);

        // The token is consumed whether or not it moved the timestamp: a link that has served its
        // purpose must not stay redeemable, and MarkEmailVerified already guards the double-click.
        var justVerified = account.MarkEmailVerified();
        request.MarkConsumed();
        await _db.SaveChangesAsync(ct);

        // No security stamp rotation, unlike a password or email change. Nothing about the account's
        // credentials changed, so ending the candidate's sessions here would log them out of the very
        // tab they are verifying from — for no security benefit.
        _logger.LogInformation(
            "Email verified for candidate account {CandidateAccountId} (newly verified: {JustVerified})",
            account.Id, justVerified);

        return Result.Success();
    }

    private async Task SendVerificationLinkAsync(
        string email, string firstName, string rawToken, CancellationToken ct)
    {
        var link = $"{_options.ConfirmBaseUrl}?token={Uri.EscapeDataString(rawToken)}";
        var body = $"""
            <p>Hi {firstName},</p>
            <p>Confirm this address to finish setting up your candidate account. You need to do this
            once before you can apply to a job.</p>
            <p><a href="{link}">Verify my email address</a></p>
            <p>This link expires in {EmailVerificationRequest.ValidHours} hours and can be used once.
            If you did not create an account, ignore this email.</p>
            """;

        try
        {
            await _emailSender.SendAsync(email, "Verify your email address", body, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the email verification link");
        }
    }

    // Same construction as CandidatePasswordResetService's and CandidateProfileService's: 256 bits of
    // randomness, URL-safe base64. Kept local rather than hoisted — see the note in those files.
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
