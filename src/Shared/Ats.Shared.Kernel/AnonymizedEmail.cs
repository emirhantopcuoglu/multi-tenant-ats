namespace Ats.Shared.Kernel;

// Stands in for an erased person's email. .invalid never resolves (RFC 2606), and the id keeps it
// unique under an email index.
public static class AnonymizedEmail
{
    public static string For(Guid id) => $"deleted-{id:N}@account.invalid";
}
