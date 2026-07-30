using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// The candidate side of authentication: register, login, and the "me" profile read. Mirrors the
// company AuthService in shape, but against the global CandidateAccounts store — no UserManager, no
// tenant, no roles.
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
        // Enforced here and not only in the frontend: the zod rule is UX, the server is the boundary.
        if (!CandidatePasswordPolicy.IsAcceptable(password))
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.PasswordTooShort);

        var normalizedEmail = CandidateAccount.NormalizeEmail(email);

        // Cheap pre-check for the common case so the caller gets a clear error rather than a raw
        // constraint violation. The unique index is still the real guard under a concurrent race.
        var emailTaken = await _db.CandidateAccounts.AnyAsync(c => c.Email == normalizedEmail);
        if (emailTaken)
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.EmailAlreadyRegistered);

        var passwordHash = _passwordHasher.Hash(password);
        // Normalized at this boundary rather than in the domain, matching how Country/City are
        // checked against their catalogue here: an unrecognised language becomes English instead of
        // failing a registration over a header the caller does not control.
        var account = CandidateAccount.Register(
            email, passwordHash, firstName, lastName, SupportedLanguages.Normalize(preferredLanguage));
        _db.CandidateAccounts.Add(account);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two concurrent registrations for the same email: both pass the pre-check, the unique
            // index rejects the loser. Translate it to the same friendly error.
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.EmailAlreadyRegistered);
        }

        // Mail the verification link, but still hand back a session. Registration deliberately does
        // not gate on this: the candidate can sign in, complete their profile and upload a CV
        // unverified — only applying is blocked (SubmitApplicationHandler). Blocking the session
        // instead would leave anyone who mistyped their address permanently locked out, with the
        // email already taken so they cannot re-register.
        //
        // The result is ignored on purpose. The account exists; a mail failure is already logged
        // inside the service, and turning it into a failed registration would be a lie about what
        // happened. The candidate can resend from the banner.
        await _emailVerification.SendAsync(account.Id);

        return Result.Success(await _sessions.IssueAsync(account));
    }

    public async Task<Result<CandidateAuthResult>> LoginAsync(string email, string password)
    {
        var normalizedEmail = CandidateAccount.NormalizeEmail(email);
        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Email == normalizedEmail);

        // One branch for "no such email" and "wrong password" so the response never reveals which.
        if (account is null)
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidCredentials);

        // Two separate decisions here, easy to conflate.
        //
        // The ORDER — lockout before the password — stops a locked account from re-locking itself.
        // Verify first and every wrong guess still runs RegisterFailedLogin; each run that reaches
        // the limit pushes the expiry further out, so an attacker who keeps hammering keeps the real
        // owner locked out indefinitely, turning a temporary lockout into the permanent denial of
        // service its expiry exists to prevent. Returning early also skips the password hash, which
        // is deliberately expensive and pointless to compute for an account that cannot sign in.
        //
        // The ANSWER — the same InvalidCredentials as a wrong password — is what keeps the response
        // from confirming an address is registered, matching the branch above. The cost is that a
        // locked-out candidate cannot tell why they are refused; the reset link is the way out and
        // clears the lockout.
        var nowUtc = DateTime.UtcNow;
        if (account.IsLockedOut(nowUtc))
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidCredentials);

        if (!_passwordHasher.Verify(account.PasswordHash, password))
        {
            // The guard the per-IP rate limiter cannot be: it counts failures against the account, so
            // it sees an attack spread over many addresses as what it is.
            account.RegisterFailedLogin(_lockout.MaxFailedAttempts, _lockout.LockoutDuration, nowUtc);
            await _db.SaveChangesAsync();
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidCredentials);
        }

        if (account.FailedLoginCount > 0)
        {
            account.ClearLockout();
            await _db.SaveChangesAsync();
        }

        // Status is deliberately not checked: a frozen account may sign in, and the SPA routes it to
        // the reactivation screen. A deleted one never gets here — the global query filter hides it.
        return Result.Success(await _sessions.IssueAsync(account));
    }

    public async Task<Result<CandidateAuthResult>> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidRefreshToken);

        var stored = await _sessions.FindAsync(refreshToken);
        if (stored is null)
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidRefreshToken);

        // The account is loaded through the filtered DbSet, so a deleted account resolves to null and
        // its tokens stop working here without this method knowing anything about deletion.
        var account = await _db.CandidateAccounts
            .FirstOrDefaultAsync(c => c.Id == stored.CandidateAccountId);
        if (account is null)
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidRefreshToken);

        // The stamp comparison is what makes a password or email change end this session too. Without
        // it, rotation below would happily mint a token carrying the *new* stamp and hand a thief a
        // working session straight through the change the owner made to lock them out.
        if (!stored.CanBeRedeemedWith(account.SecurityStamp))
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidRefreshToken);

        // Rotation: the presented token is spent, and IssueAsync writes its replacement in the same
        // SaveChanges, so a redeemed token can never be replayed.
        stored.Revoke();
        return Result.Success(await _sessions.IssueAsync(account));
    }

    public async Task<Result> LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result.Success();

        var stored = await _sessions.FindAsync(refreshToken);

        // Idempotent, and silent about whether the token existed: logout has no business telling a
        // caller whether the string they presented was ever a real session.
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
