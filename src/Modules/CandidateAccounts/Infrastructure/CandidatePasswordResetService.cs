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
    private readonly CandidatePasswordResetOptions _options;
    private readonly ILogger<CandidatePasswordResetService> _logger;

    public CandidatePasswordResetService(
        CandidateAccountsDbContext db,
        ICandidatePasswordHasher passwordHasher,
        IEmailSender emailSender,
        IOptions<CandidatePasswordResetOptions> options,
        ILogger<CandidatePasswordResetService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> RequestAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = CandidateAccount.NormalizeEmail(email ?? string.Empty);

        // The filtered DbSet keeps deleted accounts out, so their anonymized addresses are unreachable.
        var account = await _db.CandidateAccounts
            .FirstOrDefaultAsync(c => c.Email == normalizedEmail, ct);

        if (account is null)
        {
            // Success, not a 404. The whole point of this endpoint is that it is callable by someone
            // who is not signed in, so a distinguishable answer would turn it into a directory of who
            // has an account here. Logged so the absence is still visible to an operator.
            _logger.LogInformation("Password reset requested for an unregistered email");
            return Result.Success();
        }

        // A frozen account may reset its password: it can still sign in (the SPA routes it to the
        // reactivation screen), so locking it out of recovery would be a dead end.

        // A newer request supersedes older pending ones, so a forgotten earlier link cannot resurface.
        // Loaded and removed rather than bulk-deleted: an account has at most a handful of these.
        var pending = await _db.PasswordResetRequests
            .Where(r => r.CandidateAccountId == account.Id && r.ConsumedAtUtc == null)
            .ToListAsync(ct);
        _db.PasswordResetRequests.RemoveRange(pending);

        var rawToken = GenerateToken();
        _db.PasswordResetRequests.Add(PasswordResetRequest.Create(account.Id, Hash(rawToken)));
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Password reset requested for candidate account {CandidateAccountId}", account.Id);

        // Best-effort, unlike the email-change link which fails loudly. That one runs for an
        // authenticated caller who is owed a real answer; this one must return the same response
        // whether or not the address exists, and a hard failure here would leak that it does. The
        // failure is logged, not swallowed, so an SMTP outage is visible to an operator rather than
        // to an attacker.
        await SendResetLinkAsync(account.Email, rawToken, ct);

        return Result.Success();
    }

    public async Task<Result> ResetAsync(string token, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(CandidatePasswordResetErrors.InvalidToken);

        // Checked before the token is looked up so a weak password cannot burn a valid link: the
        // candidate gets to retry with the same one instead of having to request a fresh email.
        if (!CandidatePasswordPolicy.IsAcceptable(newPassword))
            return Result.Failure(CandidatePasswordResetErrors.PasswordTooShort);

        // Hashed outside the query: EF can translate a comparison against a variable, not a call to
        // a local static method.
        var tokenHash = Hash(token);
        var request = await _db.PasswordResetRequests
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

        // Unknown, expired and consumed collapse into one answer on purpose.
        if (request is null || !request.IsValid)
            return Result.Failure(CandidatePasswordResetErrors.InvalidToken);

        var account = await _db.CandidateAccounts
            .FirstOrDefaultAsync(c => c.Id == request.CandidateAccountId, ct);
        if (account is null)
            return Result.Failure(CandidatePasswordResetErrors.InvalidToken);

        // ChangePassword rotates the security stamp, which is what makes this reset actually lock an
        // attacker out: every access token dies at the next request (CandidateSecurityStampHandler)
        // and every refresh token issued under the old stamp stops redeeming (CandidateRefreshToken).
        // Recovering an account whose password was stolen would be pointless otherwise.
        account.ChangePassword(_passwordHasher.Hash(newPassword));
        request.MarkConsumed();
        await _db.SaveChangesAsync(ct);

        // The id is the only fact logged: neither the password nor its hash may appear in a log line.
        _logger.LogInformation(
            "Password reset completed for candidate account {CandidateAccountId}", account.Id);

        await NotifyPasswordResetAsync(account.Email, ct);

        return Result.Success();
    }

    private async Task SendResetLinkAsync(string email, string rawToken, CancellationToken ct)
    {
        var link = $"{_options.ResetBaseUrl}?token={Uri.EscapeDataString(rawToken)}";
        var body = $"""
            <p>A request was made to reset the password of your candidate account.</p>
            <p><a href="{link}">Choose a new password</a></p>
            <p>This link expires in 1 hour and can be used once. If you did not request this, ignore
            this email — your current password still works.</p>
            """;

        try
        {
            await _emailSender.SendAsync(email, "Reset your password", body, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the password reset email");
        }
    }

    // Best-effort by design: the reset is already committed, so a failing mail server must not turn a
    // succeeded operation into an error response. Doubles as a hijack tripwire — if the owner did not
    // do this, this notice is their signal.
    private async Task NotifyPasswordResetAsync(string email, CancellationToken ct)
    {
        const string subject = "Your password was reset";
        const string body = """
            <p>The password of your candidate account was just reset, and every signed-in session was
            ended.</p>
            <p>If you did this, no action is needed — sign in with your new password.</p>
            <p>If you did not, someone else may have access to your email — please contact us
            immediately.</p>
            """;

        try
        {
            await _emailSender.SendAsync(email, subject, body, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send the password-reset notification email");
        }
    }

    // Same construction as CandidateProfileService's and Tenants.InvitationService's: 256 bits of
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
