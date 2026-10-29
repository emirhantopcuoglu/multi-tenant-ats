namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateEmailVerificationOptions
{
    public const string SectionName = "CandidateEmailVerification";

    public string ConfirmBaseUrl { get; init; } = "http://localhost:5173/candidate/verify-email";
}
