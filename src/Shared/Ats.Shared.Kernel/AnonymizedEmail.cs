namespace Ats.Shared.Kernel;

// Stands in for an erased person's email. .invalid never resolves (RFC 2606), and the id keeps it
// unique under an email index.
public static class AnonymizedEmail
{
    private const string Domain = "@account.invalid";

    public static string For(Guid id) => $"deleted-{id:N}{Domain}";

    public static bool IsAnonymized(string email) => email.EndsWith(Domain, StringComparison.OrdinalIgnoreCase);
}
