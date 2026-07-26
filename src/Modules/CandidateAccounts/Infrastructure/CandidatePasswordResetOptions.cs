namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Where the mailed reset link points. This is a frontend URL, so it is configuration, not code — same
// pattern as CandidateEmailChangeOptions.ConfirmBaseUrl. The SPA page at that route collects the new
// password and posts it with the token from the query string.
public sealed class CandidatePasswordResetOptions
{
    public const string SectionName = "CandidatePasswordReset";

    public string ResetBaseUrl { get; init; } = "http://localhost:5173/candidate/reset-password";
}
