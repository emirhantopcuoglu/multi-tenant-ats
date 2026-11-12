using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Erases the accounts whose deletion grace period has ended. Scheduled by Hangfire.
public sealed class CandidateAccountDeletionJob
{
    private readonly CandidateAccountsDbContext _db;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<CandidateAccountDeletionJob> _logger;

    public CandidateAccountDeletionJob(
        CandidateAccountsDbContext db, IFileStorage fileStorage, ILogger<CandidateAccountDeletionJob> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    // One account failing doesn't stop the others. It stays pending, so the next run tries it again.
    public async Task EraseDueAccountsAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var dueAccountIds = await _db.CandidateAccounts
            .Where(a => a.Status == CandidateAccountStatus.PendingDeletion && a.DeletionDueAtUtc <= nowUtc)
            .OrderBy(a => a.DeletionDueAtUtc)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var erasedCount = 0;
        var failedCount = 0;
        foreach (var accountId in dueAccountIds)
        {
            try
            {
                if (await EraseAsync(accountId, nowUtc, ct))
                    erasedCount++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failedCount++;
                _logger.LogError(exception, "Failed to erase candidate account {CandidateAccountId}", accountId);
            }
            finally
            {
                // A failed save leaves its changes tracked, and the next account's save would retry them.
                _db.ChangeTracker.Clear();
            }
        }

        _logger.LogInformation(
            "Candidate account deletion erased {ErasedCount} of {DueCount} due account(s)", erasedCount, dueAccountIds.Count);

        // Fails the run so it shows on the dashboard.
        if (failedCount > 0)
            throw new InvalidOperationException($"{failedCount} candidate account(s) could not be erased.");
    }

    private async Task<bool> EraseAsync(Guid accountId, DateTime nowUtc, CancellationToken ct)
    {
        var erasure = await ConflictRetry.RunAsync(_db, async () =>
        {
            var account = await _db.CandidateAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);

            // Cancelled after the list was read.
            if (account is null || !account.IsDeletionDue(nowUtc))
                return (Erased: false, CvFileKey: (string?)null);

            // These hold real email addresses, consumed ones included.
            var emailChangeRequests = await _db.EmailChangeRequests
                .Where(r => r.CandidateAccountId == accountId)
                .ToListAsync(ct);
            _db.EmailChangeRequests.RemoveRange(emailChangeRequests);

            // Read before Erase() clears it; the file itself still has to be removed from storage.
            var cvFileKey = account.CvFileKey;
            account.Erase(nowUtc);

            await _db.SaveChangesAsync(ct);
            return (Erased: true, CvFileKey: cvFileKey);
        });

        if (erasure.CvFileKey is { } key)
            await TryDeleteCvAsync(accountId, key, ct);

        // Only the id: no personal data in the log.
        if (erasure.Erased)
            _logger.LogInformation("Candidate account {CandidateAccountId} erased", accountId);

        return erasure.Erased;
    }

    // A storage outage must not undo the erasure. Nothing points to the file now, so the orphan file
    // cleanup removes it later.
    private async Task TryDeleteCvAsync(Guid accountId, string key, CancellationToken ct)
    {
        try
        {
            await _fileStorage.DeleteAsync(key, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Failed to delete the CV of erased candidate account {CandidateAccountId}", accountId);
        }
    }
}
