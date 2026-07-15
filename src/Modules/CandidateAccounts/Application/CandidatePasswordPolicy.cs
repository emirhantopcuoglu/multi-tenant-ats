namespace Ats.Modules.CandidateAccounts.Application;

// The server-side password rule for candidate accounts. The frontend mirrors the same minimum for
// instant feedback, but this is the enforcement point: a request that bypasses the UI (curl, a buggy
// client) must hit the same wall. Length-only on purpose — composition rules (digits, symbols) push
// people toward predictable substitutions, and NIST 800-63B recommends length over complexity.
public static class CandidatePasswordPolicy
{
    public const int MinimumLength = 8;

    public static bool IsAcceptable(string password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= MinimumLength;
}
