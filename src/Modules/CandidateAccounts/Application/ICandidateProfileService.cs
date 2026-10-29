using Ats.Shared.Kernel;

namespace Ats.Modules.CandidateAccounts.Application;

public sealed record CandidateProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? Country,
    string? City,
    DateOnly? BirthDate,
    CandidateCvDto? Cv);

// No storage key here; the file is only reachable through the presigned URL endpoint.
public sealed record CandidateCvDto(string FileName, DateTime UploadedAtUtc);

// The caller owns the stream; it is only valid during the call.
public sealed record UploadCandidateCvCommand(
    Stream Content, long SizeBytes, string ContentType, string FileName);

public sealed record CandidateCvDownloadDto(string Url, int ExpiresInSeconds);

public sealed record UpdateCandidateProfileCommand(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? Country,
    string? City,
    DateOnly? BirthDate);

public sealed record ChangeCandidatePasswordCommand(string CurrentPassword, string NewPassword);

// Mails a confirmation link to the new address. The email changes only after it is clicked.
public sealed record RequestCandidateEmailChangeCommand(string NewEmail, string CurrentPassword);

// The stamp rotation ends every session, including the caller's, so the caller gets a new one.
public sealed record CandidatePasswordChangeResult(string AccessToken, string RefreshToken);

public interface ICandidateProfileService
{
    Task<Result<CandidateProfileDto>> GetAsync(Guid candidateAccountId);
    Task<Result<CandidateProfileDto>> UpdateAsync(Guid candidateAccountId, UpdateCandidateProfileCommand command);
    Task<Result<CandidatePasswordChangeResult>> ChangePasswordAsync(
        Guid candidateAccountId, ChangeCandidatePasswordCommand command);
    Task<Result> RequestEmailChangeAsync(
        Guid candidateAccountId, RequestCandidateEmailChangeCommand command);

    // Token only: the link may be opened on a device with no session.
    Task<Result> ConfirmEmailChangeAsync(string token);

    // Separate from UpdateAsync so the language toggle doesn't resend (and overwrite) the whole profile.
    Task<Result> SetPreferredLanguageAsync(Guid candidateAccountId, string language);

    // One CV per account; uploading replaces the old one.
    Task<Result<CandidateCvDto>> UploadCvAsync(Guid candidateAccountId, UploadCandidateCvCommand command);

    Task<Result> RemoveCvAsync(Guid candidateAccountId);

    Task<Result<CandidateCvDownloadDto>> GetCvDownloadUrlAsync(Guid candidateAccountId);
}
