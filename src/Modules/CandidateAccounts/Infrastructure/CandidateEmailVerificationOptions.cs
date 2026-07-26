namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Where the mailed verification link points — a frontend URL, so configuration rather than code, the
// same pattern as CandidatePasswordResetOptions.ResetBaseUrl. The SPA page at that route posts the
// token from the query string and reports the outcome.
public sealed class CandidateEmailVerificationOptions
{
    public const string SectionName = "CandidateEmailVerification";

    public string ConfirmBaseUrl { get; init; } = "http://localhost:5173/candidate/verify-email";
}
