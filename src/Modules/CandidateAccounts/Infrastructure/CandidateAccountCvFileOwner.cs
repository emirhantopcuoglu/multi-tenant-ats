using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// The CV on a candidate's profile. A deleted account has none: deleting it removes the CV.
public sealed class CandidateAccountCvFileOwner : IStoredFileOwner
{
    private readonly CandidateAccountsDbContext _db;

    public CandidateAccountCvFileOwner(CandidateAccountsDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<string>> FindReferencedAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        await _db.CandidateAccounts
            .Where(account => account.CvFileKey != null && keys.Contains(account.CvFileKey))
            .Select(account => account.CvFileKey!)
            .ToListAsync(cancellationToken);
}
