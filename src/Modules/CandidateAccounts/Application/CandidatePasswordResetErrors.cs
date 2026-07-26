using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidatePasswordResetErrors
{
    // One error for "unknown", "expired" and "already used" alike: the caller only holds a token, so
    // distinguishing the cases would tell an attacker which guesses were once real. Same reasoning as
    // CandidateProfileErrors.InvalidEmailChangeToken.
    public static readonly Error InvalidToken =
        new("candidate_password_reset.invalid_token",
            "This password reset link is invalid, expired or already used.");

    public static readonly Error PasswordTooShort =
        new("candidate_password_reset.password_too_short",
            $"Password must be at least {CandidatePasswordPolicy.MinimumLength} characters.");

    // Note there is deliberately no "email not found" error. Requesting a reset for an unknown
    // address reports success, so the endpoint cannot be used to discover who has an account.
}
