namespace Ats.Modules.CandidateAccounts.Infrastructure;

public sealed class CandidateEmailChangeOptions
{
    public const string SectionName = "CandidateEmailChange";

    public string ConfirmBaseUrl { get; init; } = "http://localhost:5173/candidate/confirm-email";
}
