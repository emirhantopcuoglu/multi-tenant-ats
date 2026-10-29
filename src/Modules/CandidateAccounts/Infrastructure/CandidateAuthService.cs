using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateAuthService : ICandidateAuthService
{
    private readonly CandidateAccountsDbContext _db;
    private readonly ICandidatePasswordHasher _passwordHasher;
    private readonly ICandidateSessionIssuer _sessions;
    private readonly ICandidateEmailVerificationService _emailVerification;
    private readonly LoginLockoutOptions _lockout;

    public CandidateAuthService(
        CandidateAccountsDbContext db,
        ICandidatePasswordHasher passwordHasher,
        ICandidateSessionIssuer sessions,
        ICandidateEmailVerificationService emailVerification,
        IOptions<LoginLockoutOptions> lockout)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _emailVerification = emailVerification;
        _lockout = lockout.Value;
    }

    public async Task<Result<CandidateAuthResult>> RegisterAsync(
        string email, string password, string firstName, string lastName, string preferredLanguage)
    {
        if (!CandidatePasswordPolicy.IsAcceptable(password))
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.PasswordTooShort);

        var normalizedEmail = CandidateAccount.NormalizeEmail(email);

        var emailTaken = await _db.CandidateAccounts.AnyAsync(c => c.Email == normalizedEmail);
        if (emailTaken)
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.EmailAlreadyRegistered);

        var passwordHash = _passwordHasher.Hash(password);
        // An unknown language falls back to English instead of failing the registration.
        var account = CandidateAccount.Register(
            email, passwordHash, firstName, lastName, SupportedLanguages.Normalize(preferredLanguage));
        _db.CandidateAccounts.Add(account);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent registration with the same email won the race.
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.EmailAlreadyRegistered);
        }

        // Login is not gated on verification, only applying is. Someone who mistyped their address
        // can still sign in and fix it. A mail failure is logged by the service and can be resent.
        await _emailVerification.SendAsync(account.Id);

        return Result.Success(await _sessions.IssueAsync(account));
    }

    public async Task<Result<CandidateAuthResult>> LoginAsync(string email, string password)
    {
        var normalizedEmail = CandidateAccount.NormalizeEmail(email);
        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Email == normalizedEmail);

        // Same error as a wrong password, so the response doesn't reveal whether the email exists.
        if (account is null)
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidCredentials);

        // Checked before the password, otherwise guesses during a lockout would keep extending it.
        var nowUtc = DateTime.UtcNow;
        if (account.IsLockedOut(nowUtc))
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidCredentials);

        if (!_passwordHasher.Verify(account.PasswordHash, password))
        {
            account.RegisterFailedLogin(_lockout.MaxFailedAttempts, _lockout.LockoutDuration, nowUtc);
            await _db.SaveChangesAsync();
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidCredentials);
        }

        if (account.FailedLoginCount > 0)
        {
            account.ClearLockout();
            await _db.SaveChangesAsync();
        }

        // Frozen accounts may sign in so they can reactivate. Deleted ones are filtered out above.
        return Result.Success(await _sessions.IssueAsync(account));
    }

    public async Task<Result<CandidateAuthResult>> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidRefreshToken);

        var stored = await _sessions.FindAsync(refreshToken);
        if (stored is null)
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidRefreshToken);

        var account = await _db.CandidateAccounts
            .FirstOrDefaultAsync(c => c.Id == stored.CandidateAccountId);
        if (account is null)
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidRefreshToken);

        // Without this, a stolen refresh token would survive a password or email change.
        if (!stored.CanBeRedeemedWith(account.SecurityStamp))
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidRefreshToken);

        stored.Revoke();
        return Result.Success(await _sessions.IssueAsync(account));
    }

    public async Task<Result> LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Success();

        var stored = await _sessions.FindAsync(refreshToken);

        if (stored is not null && stored.IsActive)
        {
            stored.Revoke();
            await _db.SaveChangesAsync();
        }

        return Result.Success();
    }

    public async Task<Result<CurrentCandidateDto>> GetCurrentCandidateAsync(Guid candidateAccountId)
    {
        var account = await _db.CandidateAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidateAccountId);

        if (account is null)
            return Result.Failure<CurrentCandidateDto>(CandidateAuthErrors.NotFound);

        return Result.Success(new CurrentCandidateDto(
            account.Id, account.Email, account.FirstName, account.LastName, account.Status,
            account.IsEmailVerified));
    }
}
