using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidateEmailVerificationErrors
{
    // Unknown, expired and already-consumed collapse into one code. Distinguishing them would tell an
    // attacker holding a guessed token whether it ever existed, and the candidate's next step is the
    // same in all three cases: ask for a fresh link.
    public static readonly Error InvalidToken =
        new("candidate_email_verification.invalid_token",
            "This verification link is invalid or has expired. Request a new one.");

    public static readonly Error AlreadyVerified =
        new("candidate_email_verification.already_verified",
            "This email address is already verified.");
}
