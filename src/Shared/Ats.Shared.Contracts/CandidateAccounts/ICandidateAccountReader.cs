namespace Ats.Shared.Contracts.CandidateAccounts;

// IsEmailVerified, IsActive and the CV fields ride along because SubmitApplicationHandler already
// loads this record. IsActive is positive, so any new status is treated as inactive. Deleted
// accounts are filtered out. The CV key is shared so Applications can copy the file in storage
// instead of streaming it.
public sealed record CandidateAccountSummary(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsEmailVerified,
    bool IsActive,
    string? CvFileKey,
    string? CvFileName);

public interface ICandidateAccountReader
{
    Task<CandidateAccountSummary?> GetByIdAsync(Guid id, CancellationToken ct = default);

    // By email because the events carry the address. Falls back to the default language. Read live,
    // so a language switch applies to the next email.
    Task<string> GetPreferredLanguageByEmailAsync(string email, CancellationToken ct = default);
}
