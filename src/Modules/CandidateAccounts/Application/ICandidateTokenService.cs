namespace Ats.Modules.CandidateAccounts.Application;

// Mints the candidate access token. Separated from the auth service (SRP + testability) and kept as an
// abstraction so the JWT/crypto details stay in Infrastructure. Takes primitives rather than the
// CandidateAccount entity: the token only needs the identity (id, email) and the account's current
// security stamp, not the whole aggregate.
public interface ICandidateTokenService
{
    string GenerateAccessToken(Guid candidateAccountId, string email, Guid securityStamp);
}
