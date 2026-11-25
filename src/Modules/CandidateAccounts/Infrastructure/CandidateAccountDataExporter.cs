using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateAccountDataExporter : ICandidateDataExporter
{
    private readonly CandidateAccountsDbContext _db;

    public CandidateAccountDataExporter(CandidateAccountsDbContext db) => _db = db;

    public string Section => "account";

    public async Task<object> ExportAsync(Guid candidateAccountId, CancellationToken cancellationToken = default) =>
        await _db.CandidateAccounts
            .AsNoTracking()
            .Where(a => a.Id == candidateAccountId)
            .Select(a => new
            {
                a.Id,
                a.Email,
                a.FirstName,
                a.LastName,
                a.PhoneNumber,
                a.Country,
                a.City,
                a.BirthDate,
                a.PreferredLanguage,
                a.CvFileName,
                a.CvUploadedAtUtc,
                Status = a.Status.ToString(),
                a.CreatedAtUtc,
                a.EmailVerifiedAtUtc
            })
            .SingleAsync(cancellationToken);
}
