using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Contracts.CandidateAccounts;
using Ats.Shared.Kernel;
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
            : new CandidateAccountSummary(
                account.Id, account.Email, account.FirstName, account.LastName, account.IsEmailVerified);
    }

    public async Task<string> GetPreferredLanguageByEmailAsync(string email, CancellationToken ct = default)
    {
        // Normalized before comparing, the same way Register() normalizes before storing: the address
        // on an integration event came from an apply form and may differ only in case.
        var normalized = CandidateAccount.NormalizeEmail(email);

        // Projects the single column rather than materialising the account: this runs on every
        // outgoing email and none of the rest of the row is wanted.
        var language = await _db.CandidateAccounts
            .AsNoTracking()
            .Where(a => a.Email == normalized)
            .Select(a => a.PreferredLanguage)
            .FirstOrDefaultAsync(ct);

        return SupportedLanguages.Normalize(language);
    }
}
