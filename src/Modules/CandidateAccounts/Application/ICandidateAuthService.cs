using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public sealed record CandidateAuthResult(string AccessToken, string RefreshToken);

// Status and IsEmailVerified let the SPA show the reactivation screen and the verify banner.
public sealed record CurrentCandidateDto(
    Guid Id, string Email, string FirstName, string LastName, CandidateAccountStatus Status,
    bool IsEmailVerified);

public interface ICandidateAuthService
{
    // The verification email is sent during this call, in this language.
    Task<Result<CandidateAuthResult>> RegisterAsync(
        string email, string password, string firstName, string lastName, string preferredLanguage);
    Task<Result<CandidateAuthResult>> LoginAsync(string email, string password);

    /// Exchanges a refresh token for a new pair and revokes the old one. Tokens issued before the last security stamp rotation are rejected.
   
    Task<Result<CandidateAuthResult>> RefreshAsync(string refreshToken);

    // Revokes the presented refresh token. Succeeds even if it was already unusable.
    Task<Result> LogoutAsync(string refreshToken);

    Task<Result<CurrentCandidateDto>> GetCurrentCandidateAsync(Guid candidateAccountId);
}
