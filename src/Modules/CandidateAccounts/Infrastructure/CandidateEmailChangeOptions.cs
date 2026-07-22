namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Where the mailed confirmation link points. This is a frontend URL, so it is configuration, not
// code: the SPA page at that route posts the token to the confirm endpoint. Same pattern as
// InvitationOptions.AcceptBaseUrl.
public sealed class CandidateEmailChangeOptions
{
    public const string SectionName = "CandidateEmailChange";

    public string ConfirmBaseUrl { get; init; } = "http://localhost:5173/candidate/confirm-email";
}
