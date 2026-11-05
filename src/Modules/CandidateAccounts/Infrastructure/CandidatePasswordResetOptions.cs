namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidatePasswordResetOptions
{
    public const string SectionName = "CandidatePasswordReset";

    public string ResetBaseUrl { get; init; } = string.Empty;
}
