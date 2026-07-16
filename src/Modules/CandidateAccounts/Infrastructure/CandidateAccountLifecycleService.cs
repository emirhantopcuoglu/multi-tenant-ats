using Ats.Modules.CandidateAccounts.Application;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateAccountLifecycleService : ICandidateAccountLifecycleService
{
    private readonly CandidateAccountsDbContext _db;
    private readonly ICandidatePasswordHasher _passwordHasher;
    private readonly ILogger<CandidateAccountLifecycleService> _logger;

    public CandidateAccountLifecycleService(
        CandidateAccountsDbContext db,
        ICandidatePasswordHasher passwordHasher,
        ILogger<CandidateAccountLifecycleService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<Result> FreezeAsync(Guid candidateAccountId)
    {
        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure(CandidateAccountLifecycleErrors.NotFound);

        try
        {
            account.Freeze();
        }
        catch (InvalidOperationException rejectedTransition)
        {
            return Result.Failure(CandidateAccountLifecycleErrors.InvalidState(rejectedTransition.Message));
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Candidate account {CandidateAccountId} frozen", candidateAccountId);
        return Result.Success();
    }

    public async Task<Result> ReactivateAsync(Guid candidateAccountId)
    {
        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure(CandidateAccountLifecycleErrors.NotFound);

        try
        {
            account.Reactivate();
        }
        catch (InvalidOperationException rejectedTransition)
        {
            return Result.Failure(CandidateAccountLifecycleErrors.InvalidState(rejectedTransition.Message));
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Candidate account {CandidateAccountId} reactivated", candidateAccountId);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid candidateAccountId, DeleteCandidateAccountCommand command)
    {
        var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
        if (account is null)
            return Result.Failure(CandidateAccountLifecycleErrors.NotFound);

        // Ownership re-proof before the most destructive action on the account: a stolen token must
        // not be able to erase someone's identity.
        if (!_passwordHasher.Verify(account.PasswordHash, command.CurrentPassword))
            return Result.Failure(CandidateAccountLifecycleErrors.InvalidCurrentPassword);

        // The anonymization must reach every table holding personal data: pending AND consumed
        // email change requests carry real addresses, so all of them go — audit value loses to the
        // right to erasure here.
        var emailChangeRequests = await _db.EmailChangeRequests
            .Where(r => r.CandidateAccountId == account.Id)
            .ToListAsync();
        _db.EmailChangeRequests.RemoveRange(emailChangeRequests);

        try
        {
            account.Delete();
        }
        catch (InvalidOperationException rejectedTransition)
        {
            return Result.Failure(CandidateAccountLifecycleErrors.InvalidState(rejectedTransition.Message));
        }

        await _db.SaveChangesAsync();

        // The id is deliberately the only fact logged: after this line the account has no personal
        // data left anywhere, and the log must not become the place it survives.
        _logger.LogInformation("Candidate account {CandidateAccountId} deleted", candidateAccountId);
        return Result.Success();
    }
}
