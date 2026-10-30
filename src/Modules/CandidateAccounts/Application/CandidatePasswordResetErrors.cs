using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidatePasswordResetErrors
{
    public static readonly Error InvalidToken =
        new("candidate_password_reset.invalid_token",
            "This password reset link is invalid, expired or already used.");

    public static readonly Error PasswordTooShort =
        new("candidate_password_reset.password_too_short",
            $"Password must be at least {CandidatePasswordPolicy.MinimumLength} characters.");

}
