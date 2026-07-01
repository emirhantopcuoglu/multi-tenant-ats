using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Microsoft.AspNetCore.Identity;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Adapts ASP.NET Identity's PasswordHasher (PBKDF2, per-password salt, versioned format) to our
// subject-less port. Identity's API is per-user because it can upgrade a user's stored hash on login;
// we don't use that, so the "user" argument is irrelevant — the default hasher ignores it. Passing
// null keeps us from inventing a throwaway CandidateAccount just to satisfy the signature.
public sealed class CandidatePasswordHasher : ICandidatePasswordHasher
{
    private readonly IPasswordHasher<CandidateAccount> _inner;

    public CandidatePasswordHasher(IPasswordHasher<CandidateAccount> inner)
    {
        _inner = inner;
    }

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        _inner.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
