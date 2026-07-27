using Ats.Modules.CandidateAccounts.Application;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateAccountLifecycleService : ICandidateAccountLifecycleService
{
    private readonly CandidateAccountsDbContext _db;
    private readonly ICandidatePasswordHasher _passwordHasher;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<CandidateAccountLifecycleService> _logger;

    public CandidateAccountLifecycleService(
        CandidateAccountsDbContext db,
        ICandidatePasswordHasher passwordHasher,
        IFileStorage fileStorage,
        ILogger<CandidateAccountLifecycleService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _fileStorage = fileStorage;
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

        // Read before Delete() clears it. The CV is personal data living outside the database, so
        // erasure has to follow it into object storage — a row that no longer names the file does
        // not make the file go away.
        var cvFileKey = account.CvFileKey;

        try
        {
            account.Delete();
        }
        catch (InvalidOperationException rejectedTransition)
        {
            return Result.Failure(CandidateAccountLifecycleErrors.InvalidState(rejectedTransition.Message));
        }

        await _db.SaveChangesAsync();

        // After the commit: if the deletion had failed, the account would still be live and would
        // still need its CV. Best-effort, because a storage outage must not block an erasure the
        // database has already accepted — the failure is logged loudly enough to be swept later.
        if (cvFileKey is not null)
        {
            try
            {
                await _fileStorage.DeleteAsync(cvFileKey);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to delete the CV of erased candidate account {CandidateAccountId}",
                    candidateAccountId);
            }
        }

        // The id is deliberately the only fact logged: after this line the account has no personal
        // data left anywhere, and the log must not become the place it survives.
        _logger.LogInformation("Candidate account {CandidateAccountId} deleted", candidateAccountId);
        return Result.Success();
    }
}
