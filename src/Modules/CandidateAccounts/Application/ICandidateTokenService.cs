namespace Ats.Modules.CandidateAccounts.Application;

// Mints the candidate access token. Separated from the auth service (SRP + testability) and kept as an
// abstraction so the JWT/crypto details stay in Infrastructure. Takes primitives rather than the
// CandidateAccount entity: the token only needs the identity (id, email) and the account's current
// security stamp, not the whole aggregate.
public interface ICandidateTokenService
{
    string GenerateAccessToken(Guid candidateAccountId, string email, Guid securityStamp);

    /// <summary>
    /// Returns opaque random material for a refresh token. Unlike the access token this carries no
    /// claims and is never parsed — only its hash is stored and compared — so it takes no arguments.
    /// </summary>
    string GenerateRefreshToken();
}
