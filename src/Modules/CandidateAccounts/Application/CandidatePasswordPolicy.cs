namespace Ats.Modules.CandidateAccounts.Application;

public static class CandidatePasswordPolicy
{
    public const int MinimumLength = 8;

    public static bool IsAcceptable(string password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= MinimumLength;
}
