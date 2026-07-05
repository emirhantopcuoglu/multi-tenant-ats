using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// The candidate side of authentication: register, login, and the "me" profile read. Mirrors the
// company AuthService in shape, but against the global CandidateAccounts store — no UserManager, no
// tenant, no roles.
public sealed class CandidateAuthService : ICandidateAuthService
{
    private readonly CandidateAccountsDbContext _db;
    private readonly ICandidatePasswordHasher _passwordHasher;
    private readonly ICandidateTokenService _tokenService;

    public CandidateAuthService(
        CandidateAccountsDbContext db,
        ICandidatePasswordHasher passwordHasher,
        ICandidateTokenService tokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<Result<CandidateAuthResult>> RegisterAsync(
        string email, string password, string firstName, string lastName)
    {
        var normalizedEmail = CandidateAccount.NormalizeEmail(email);

        // Cheap pre-check for the common case so the caller gets a clear error rather than a raw
        // constraint violation. The unique index is still the real guard under a concurrent race.
        var emailTaken = await _db.CandidateAccounts.AnyAsync(c => c.Email == normalizedEmail);
        if (emailTaken)
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.EmailAlreadyRegistered);

        var passwordHash = _passwordHasher.Hash(password);
        var account = CandidateAccount.Register(email, passwordHash, firstName, lastName);
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

        var token = _tokenService.GenerateAccessToken(account.Id, account.Email);
        return Result.Success(new CandidateAuthResult(token));
    }

    public async Task<Result<CandidateAuthResult>> LoginAsync(string email, string password)
    {
        var normalizedEmail = CandidateAccount.NormalizeEmail(email);
        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Email == normalizedEmail);

        // One branch for "no such email" and "wrong password" so the response never reveals which.
        if (account is null || !_passwordHasher.Verify(account.PasswordHash, password))
            return Result.Failure<CandidateAuthResult>(CandidateAuthErrors.InvalidCredentials);

        var token = _tokenService.GenerateAccessToken(account.Id, account.Email);
        return Result.Success(new CandidateAuthResult(token));
    }

    public async Task<Result<CurrentCandidateDto>> GetCurrentCandidateAsync(Guid candidateAccountId)
    {
        var account = await _db.CandidateAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidateAccountId);

        if (account is null)
            return Result.Failure<CurrentCandidateDto>(CandidateAuthErrors.NotFound);

        return Result.Success(
            new CurrentCandidateDto(account.Id, account.Email, account.FirstName, account.LastName));
    }

    public async Task<Result<CurrentCandidateDto>> UpdateProfileAsync(
        Guid candidateAccountId, string firstName, string lastName)
    {
        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure<CurrentCandidateDto>(CandidateAuthErrors.NotFound);

        account.UpdateProfile(firstName, lastName);
        await _db.SaveChangesAsync();

        return Result.Success(
            new CurrentCandidateDto(account.Id, account.Email, account.FirstName, account.LastName));
    }
}
