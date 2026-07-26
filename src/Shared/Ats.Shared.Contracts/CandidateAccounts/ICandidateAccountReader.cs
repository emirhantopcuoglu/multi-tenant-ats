namespace Ats.Shared.Contracts.CandidateAccounts;

// IsEmailVerified travels on the summary rather than behind a second port method: the Applications
// module needs it on exactly the path that already loads this record (SubmitApplicationHandler), so a
// separate call would be a second round trip for a field the first one can carry.
//
// A bool, not the timestamp: the only question outside this module is "may they apply?". When the
// address was proven stays the CandidateAccounts module's business.
public sealed record CandidateAccountSummary(
    Guid Id, string Email, string FirstName, string LastName, bool IsEmailVerified);

public interface ICandidateAccountReader
{
    Task<CandidateAccountSummary?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
