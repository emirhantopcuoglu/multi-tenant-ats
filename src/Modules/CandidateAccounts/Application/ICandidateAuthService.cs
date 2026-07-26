using Ats.Modules.CandidateAccounts.Domain;
using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

// What login/register/refresh hand back. The access token is short-lived (minutes); the refresh token
// is the long-lived half the client redeems for a new pair, and it rotates on every redemption.
public sealed record CandidateAuthResult(string AccessToken, string RefreshToken);

// The signed-in candidate's profile for the marketplace UI. The JWT already carries the id and email,
// but not the display name — this fills that gap, mirroring the company side's CurrentUserDto.
// Status is how the SPA learns it should show a frozen account the reactivation screen instead of
// the candidate area; a deleted account can never reach this endpoint (query filter + dead stamp),
// so the value is only ever Active or Frozen in practice.
public sealed record CurrentCandidateDto(
    Guid Id, string Email, string FirstName, string LastName, CandidateAccountStatus Status);

// The candidate side of authentication. A separate service from the company IAuthService on purpose:
// candidates are a different identity (global, no tenant, no roles), so sharing one service would mean
// one type juggling two unrelated auth models.
public interface ICandidateAuthService
{
    Task<Result<CandidateAuthResult>> RegisterAsync(string email, string password, string firstName, string lastName);
    Task<Result<CandidateAuthResult>> LoginAsync(string email, string password);

    /// <summary>
    /// Exchanges a refresh token for a fresh pair, revoking the one presented (rotation). Fails if the
    /// token is unknown, expired, already spent, or was issued before the account's security stamp
    /// rotated — a password or email change ends outstanding refresh tokens, not just access tokens.
    /// </summary>
    Task<Result<CandidateAuthResult>> RefreshAsync(string refreshToken);

    /// <summary>Revokes the presented refresh token. Succeeds even if it was already unusable.</summary>
    Task<Result> LogoutAsync(string refreshToken);

    Task<Result<CurrentCandidateDto>> GetCurrentCandidateAsync(Guid candidateAccountId);
}
