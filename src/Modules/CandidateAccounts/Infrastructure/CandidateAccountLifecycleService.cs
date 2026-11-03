using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
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
        var frozen = await ChangeStatusAsync(candidateAccountId, account => account.Freeze());
        if (frozen.IsSuccess)
            _logger.LogInformation("Candidate account {CandidateAccountId} frozen", candidateAccountId);

        return frozen;
    }

    public async Task<Result> ReactivateAsync(Guid candidateAccountId)
    {
        var reactivated = await ChangeStatusAsync(candidateAccountId, account => account.Reactivate());
        if (reactivated.IsSuccess)
            _logger.LogInformation("Candidate account {CandidateAccountId} reactivated", candidateAccountId);

        return reactivated;
    }

    private Task<Result> ChangeStatusAsync(Guid candidateAccountId, Action<CandidateAccount> transition) =>
        ConflictRetry.RunAsync(_db, async () =>
        {
            var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
            if (account is null)
                return Result.Failure(CandidateAccountLifecycleErrors.NotFound);

            try
            {
                transition(account);
            }
            catch (InvalidOperationException rejectedTransition)
            {
                return Result.Failure(CandidateAccountLifecycleErrors.InvalidState(rejectedTransition.Message));
            }

            await _db.SaveChangesAsync();
            return Result.Success();
        });

    public async Task<Result> DeleteAsync(Guid candidateAccountId, DeleteCandidateAccountCommand command)
    {
        var deleted = await ConflictRetry.RunAsync(_db, async () =>
        {
            var account = await _db.CandidateAccounts.FirstOrDefaultAsync(c => c.Id == candidateAccountId);
            if (account is null)
                return Result.Failure<string?>(CandidateAccountLifecycleErrors.NotFound);

            // A stolen session alone must not be able to delete the account.
            if (!_passwordHasher.Verify(account.PasswordHash, command.CurrentPassword))
                return Result.Failure<string?>(CandidateAccountLifecycleErrors.InvalidCurrentPassword);

            // These hold real email addresses, consumed ones included.
            var emailChangeRequests = await _db.EmailChangeRequests
                .Where(r => r.CandidateAccountId == account.Id)
                .ToListAsync();
            _db.EmailChangeRequests.RemoveRange(emailChangeRequests);

            // Read before Delete() clears it; the file itself still has to be removed from storage.
            var cvFileKey = account.CvFileKey;

            try
            {
                account.Delete();
            }
            catch (InvalidOperationException rejectedTransition)
            {
                return Result.Failure<string?>(CandidateAccountLifecycleErrors.InvalidState(rejectedTransition.Message));
            }

            await _db.SaveChangesAsync();
            return Result.Success(cvFileKey);
        });

        if (deleted.IsFailure)
            return deleted;

        // After the save, and best-effort: a storage outage must not undo the deletion.
        if (deleted.Value is { } cvFileKey)
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

        // Only the id: no personal data in the log.
        _logger.LogInformation("Candidate account {CandidateAccountId} deleted", candidateAccountId);
        return Result.Success();
    }
}
