using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

// What login/register hand back. Only an access token for now: refresh-token rotation exists on the
// company side but is deliberately out of scope for this step (it needs its own store and endpoints).
public sealed record CandidateAuthResult(string AccessToken);

// The signed-in candidate's profile for the marketplace UI. The JWT already carries the id and email,
// but not the display name — this fills that gap, mirroring the company side's CurrentUserDto.
public sealed record CurrentCandidateDto(Guid Id, string Email, string FirstName, string LastName);

// The candidate side of authentication. A separate service from the company IAuthService on purpose:
// candidates are a different identity (global, no tenant, no roles), so sharing one service would mean
// one type juggling two unrelated auth models.
public interface ICandidateAuthService
{
    Task<Result<CandidateAuthResult>> RegisterAsync(string email, string password, string firstName, string lastName);
    Task<Result<CandidateAuthResult>> LoginAsync(string email, string password);
    Task<Result<CurrentCandidateDto>> GetCurrentCandidateAsync(Guid candidateAccountId);
    Task<Result<CurrentCandidateDto>> UpdateProfileAsync(Guid candidateAccountId, string firstName, string lastName);
}
