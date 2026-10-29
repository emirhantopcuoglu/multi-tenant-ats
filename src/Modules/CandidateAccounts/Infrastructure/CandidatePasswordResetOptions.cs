namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidatePasswordResetOptions
{
    public const string SectionName = "CandidatePasswordReset";

    public string ResetBaseUrl { get; init; } = "http://localhost:5173/candidate/reset-password";
}
