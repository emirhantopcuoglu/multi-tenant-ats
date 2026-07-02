using Ats.Shared.Contracts.CandidateAccounts;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateAccountReader : ICandidateAccountReader
{
    private readonly CandidateAccountsDbContext _db;

    public CandidateAccountReader(CandidateAccountsDbContext db)
    {
        _db = db;
    }

    public async Task<CandidateAccountSummary?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var account = await _db.CandidateAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return account is null
            ? null
            : new CandidateAccountSummary(account.Id, account.Email, account.FirstName, account.LastName);
    }
}
