using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public sealed record RequestCandidateAccountDeletionCommand(string CurrentPassword);

public interface ICandidateAccountLifecycleService
{
    Task<Result> FreezeAsync(Guid candidateAccountId);
    Task<Result> ReactivateAsync(Guid candidateAccountId);
    // Returns when the account will be erased; until then the candidate can sign in and cancel.
    Task<Result<DateTime>> RequestDeletionAsync(Guid candidateAccountId, RequestCandidateAccountDeletionCommand command);
    Task<Result> CancelDeletionAsync(Guid candidateAccountId);
}
