namespace Ats.Modules.CandidateAccounts.Application;

// Custom claim names carried by candidate access tokens, beyond the registered JWT ones (sub, email).
// A constant shared by the token service (which writes the claim) and the authorization handler
// (which reads it back) so the two can never drift apart on the spelling.
public static class CandidateClaims
{
    public const string SecurityStamp = "security_stamp";
}
