namespace Ats.Modules.CandidateAccounts.Application;

public interface ICandidateTokenService
{
    string GenerateAccessToken(Guid candidateAccountId, string email, Guid securityStamp);

    // Returns a random, opaque refresh token. Only its hash is stored.
    string GenerateRefreshToken();
}
