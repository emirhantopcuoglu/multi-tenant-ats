using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Microsoft.AspNetCore.Identity;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Wraps Identity's PasswordHasher. The default hasher ignores the user argument, so null is safe.
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
