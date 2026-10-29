using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidateAccountLifecycleErrors
{
    public static readonly Error NotFound =
        new("candidate_account.not_found", "Candidate account not found.");

    public static readonly Error InvalidCurrentPassword =
        new("candidate_account.invalid_current_password", "The current password is incorrect.");

    // A rejected state transition, returned as a 400 with its message.
    public static Error InvalidState(string message) =>
        new("candidate_account.invalid_state", message);
}
