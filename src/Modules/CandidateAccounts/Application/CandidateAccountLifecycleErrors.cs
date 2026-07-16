using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidateAccountLifecycleErrors
{
    public static readonly Error NotFound =
        new("candidate_account.not_found", "Candidate account not found.");

    public static readonly Error InvalidCurrentPassword =
        new("candidate_account.invalid_current_password", "The current password is incorrect.");

    // Wraps a rejected state transition (freezing a frozen account, reactivating an active one) so
    // the API answers 400 with the rule that failed instead of a generic 500.
    public static Error InvalidState(string message) =>
        new("candidate_account.invalid_state", message);
}
