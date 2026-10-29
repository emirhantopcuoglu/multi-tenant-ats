namespace Ats.Modules.CandidateAccounts.Application;

public interface ICandidateTokenService
{
    string GenerateAccessToken(Guid candidateAccountId, string email, Guid securityStamp);

    /// <summary>Returns a random, opaque refresh token. Only its hash is stored.</summary>
    string GenerateRefreshToken();
}
