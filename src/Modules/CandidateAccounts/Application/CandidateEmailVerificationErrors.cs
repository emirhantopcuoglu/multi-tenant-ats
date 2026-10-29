using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidateEmailVerificationErrors
{
    // One error for unknown, expired and used tokens, so guesses reveal nothing.
    public static readonly Error InvalidToken =
        new("candidate_email_verification.invalid_token",
            "This verification link is invalid or has expired. Request a new one.");

    public static readonly Error AlreadyVerified =
        new("candidate_email_verification.already_verified",
            "This email address is already verified.");
}
