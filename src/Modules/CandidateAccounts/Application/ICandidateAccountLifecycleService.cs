using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public sealed record DeleteCandidateAccountCommand(string CurrentPassword);

public interface ICandidateAccountLifecycleService
{
    Task<Result> FreezeAsync(Guid candidateAccountId);
    Task<Result> ReactivateAsync(Guid candidateAccountId);
    Task<Result> DeleteAsync(Guid candidateAccountId, DeleteCandidateAccountCommand command);
}
