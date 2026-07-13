using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

// The full profile as the settings page needs it — richer than CurrentCandidateDto, which stays a
// minimal "who am I" payload for the auth context (header display) and deliberately does not grow
// with every new profile field.
public sealed record CandidateProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? Country,
    string? City,
    DateOnly? BirthDate);

public sealed record UpdateCandidateProfileCommand(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? Country,
    string? City,
    DateOnly? BirthDate);

// Profile management split from ICandidateAuthService on purpose: auth answers "who are you"
// (register/login/me) while this owns the profile resource. Password and email changes land here in
// later steps, which would have bloated the auth service well past one responsibility.
public interface ICandidateProfileService
{
    Task<Result<CandidateProfileDto>> GetAsync(Guid candidateAccountId);
    Task<Result<CandidateProfileDto>> UpdateAsync(Guid candidateAccountId, UpdateCandidateProfileCommand command);
}
