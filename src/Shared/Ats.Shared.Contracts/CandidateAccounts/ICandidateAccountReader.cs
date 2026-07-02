namespace Ats.Shared.Contracts.CandidateAccounts;

public sealed record CandidateAccountSummary(Guid Id, string Email, string FirstName, string LastName);

public interface ICandidateAccountReader
{
    Task<CandidateAccountSummary?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
