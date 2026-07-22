using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

// Deletion re-proves ownership with the current password, exactly like the password and email
// changes: it is the most destructive action on the account, so a stolen token alone must not be
// enough to trigger it.
public sealed record DeleteCandidateAccountCommand(string CurrentPassword);

// Lifecycle (freeze/reactivate/delete) is its own service, not more methods on the profile service:
// the profile service edits what the account SAYS, this one changes whether the account EXISTS —
// the same single-responsibility split that separated profile from auth.
public interface ICandidateAccountLifecycleService
{
    Task<Result> FreezeAsync(Guid candidateAccountId);
    Task<Result> ReactivateAsync(Guid candidateAccountId);
    Task<Result> DeleteAsync(Guid candidateAccountId, DeleteCandidateAccountCommand command);
}
