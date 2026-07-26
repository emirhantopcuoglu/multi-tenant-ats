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

public sealed record ChangeCandidatePasswordCommand(string CurrentPassword, string NewPassword);

// Phase one of the two-phase email change: record the intent and mail a verification link to the
// NEW address. CurrentPassword re-proves ownership exactly like the password change does — a stolen
// token alone must not be able to redirect the login identity to an attacker's mailbox.
public sealed record RequestCandidateEmailChangeCommand(string NewEmail, string CurrentPassword);

// Hands back a fresh token pair: the change rotates the security stamp, which kills every issued
// token — including the one this very request arrived with, AND the refresh token behind it (see
// CandidateRefreshToken). Without a replacement pair the candidate would be silently logged out by
// their own successful password change. Every other session stays dead, which is the point.
public sealed record CandidatePasswordChangeResult(string AccessToken, string RefreshToken);

// Profile management split from ICandidateAuthService on purpose: auth answers "who are you"
// (register/login/me) while this owns the profile resource. Password and email changes land here in
// later steps, which would have bloated the auth service well past one responsibility.
public interface ICandidateProfileService
{
    Task<Result<CandidateProfileDto>> GetAsync(Guid candidateAccountId);
    Task<Result<CandidateProfileDto>> UpdateAsync(Guid candidateAccountId, UpdateCandidateProfileCommand command);
    Task<Result<CandidatePasswordChangeResult>> ChangePasswordAsync(
        Guid candidateAccountId, ChangeCandidatePasswordCommand command);
    Task<Result> RequestEmailChangeAsync(
        Guid candidateAccountId, RequestCandidateEmailChangeCommand command);

    // Token-only on purpose: the confirmer clicks a mailed link, possibly on another device with no
    // session, so this cannot demand authentication — the token itself is the proof.
    Task<Result> ConfirmEmailChangeAsync(string token);
}
