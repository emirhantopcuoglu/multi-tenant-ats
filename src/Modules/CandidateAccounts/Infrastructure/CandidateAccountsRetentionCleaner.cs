using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Expired candidate sessions and single-use requests. The email change ones hold real addresses.
public sealed class CandidateAccountsRetentionCleaner : IRetentionCleaner
{
    private readonly CandidateAccountsDbContext _db;

    public CandidateAccountsRetentionCleaner(CandidateAccountsDbContext db) => _db = db;

    public async Task<int> CleanAsync(RetentionCutoffs cutoffs, CancellationToken cancellationToken = default)
    {
        var expiredBefore = cutoffs.ExpiredTokenBeforeUtc;

        var sessions = await _db.CandidateRefreshTokens
            .Where(t => t.ExpiresAtUtc < expiredBefore)
            .ExecuteDeleteAsync(cancellationToken);
        var emailChanges = await _db.EmailChangeRequests
            .Where(r => r.ExpiresAtUtc < expiredBefore)
            .ExecuteDeleteAsync(cancellationToken);
        var emailVerifications = await _db.EmailVerificationRequests
            .Where(r => r.ExpiresAtUtc < expiredBefore)
            .ExecuteDeleteAsync(cancellationToken);
        var passwordResets = await _db.PasswordResetRequests
            .Where(r => r.ExpiresAtUtc < expiredBefore)
            .ExecuteDeleteAsync(cancellationToken);

        return sessions + emailChanges + emailVerifications + passwordResets;
    }
}
