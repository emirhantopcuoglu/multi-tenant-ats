using Asp.Versioning;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ats.Modules.CandidateAccounts.Api;

// The candidate's own profile resource. Separate from CandidateAuthController so auth stays
// register/login/me and this controller can grow the profile area (password change, email change)
// without turning the auth surface into a grab bag.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/candidate/profile")]
[Authorize(Policy = Policies.CandidateOnly)]
public sealed class CandidateProfileController : ControllerBase
{
    private readonly ICandidateProfileService _profileService;
    private readonly ICurrentUser _currentUser;

    public CandidateProfileController(ICandidateProfileService profileService, ICurrentUser currentUser)
    {
        _profileService = profileService;
        _currentUser = currentUser;
    }

    public sealed record UpdateCandidateProfileRequest(
        string FirstName,
        string LastName,
        string? PhoneNumber,
        string? Country,
        string? City,
        DateOnly? BirthDate);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var result = await _profileService.GetAsync(candidateAccountId);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateCandidateProfileRequest request)
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var command = new UpdateCandidateProfileCommand(
            request.FirstName, request.LastName, request.PhoneNumber,
            request.Country, request.City, request.BirthDate);

        var result = await _profileService.UpdateAsync(candidateAccountId, command);

        if (result.IsSuccess)
            return Ok(result.Value);

        // Only "account gone" is a 404; every other failure is the caller's input.
        return result.Error == CandidateProfileErrors.NotFound
            ? NotFound(new { result.Error.Code, result.Error.Message })
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    public sealed record SetPreferredLanguageRequest(string Language);

    // Its own route rather than a field on PUT /profile: the SPA fires this from the header toggle on
    // any screen, and a full profile PUT from a page that is not the profile form would happily write
    // back a stale copy of every other field.
    [HttpPut("language")]
    public async Task<IActionResult> SetPreferredLanguage(SetPreferredLanguageRequest request)
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var result = await _profileService.SetPreferredLanguageAsync(candidateAccountId, request.Language);

        if (result.IsSuccess)
            return NoContent();

        return result.Error == CandidateProfileErrors.NotFound
            ? NotFound(new { result.Error.Code, result.Error.Message })
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    public sealed record ChangeCandidatePasswordRequest(string CurrentPassword, string NewPassword);

    // Rate limited like login even though it is authenticated: the current-password check makes this
    // endpoint a password oracle for anyone holding a stolen token, so it gets the same brute-force
    // guard as the anonymous auth endpoints.
    [HttpPost("password")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ChangePassword(ChangeCandidatePasswordRequest request)
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var command = new ChangeCandidatePasswordCommand(request.CurrentPassword, request.NewPassword);
        var result = await _profileService.ChangePasswordAsync(candidateAccountId, command);

        if (result.IsSuccess)
            return Ok(result.Value);

        return result.Error == CandidateProfileErrors.NotFound
            ? NotFound(new { result.Error.Code, result.Error.Message })
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    public sealed record RequestEmailChangeRequest(string NewEmail, string CurrentPassword);

    public sealed record ConfirmEmailChangeRequest(string Token);

    // Rate limited for the same reason as the password endpoint: verifying the current password
    // makes this an oracle for anyone holding a stolen token.
    [HttpPost("email")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> RequestEmailChange(RequestEmailChangeRequest request)
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var command = new RequestCandidateEmailChangeCommand(request.NewEmail, request.CurrentPassword);
        var result = await _profileService.RequestEmailChangeAsync(candidateAccountId, command);

        if (result.IsSuccess)
            return Ok();

        return result.Error == CandidateProfileErrors.NotFound
            ? NotFound(new { result.Error.Code, result.Error.Message })
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    // Anonymous inside an [Authorize] controller on purpose: the confirmer clicks a mailed link,
    // possibly on a device with no session, and the 256-bit single-use token is the proof of
    // ownership. Rate limited so token guessing is throttled on top of being cryptographically
    // hopeless.
    [HttpPost("email/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ConfirmEmailChange(ConfirmEmailChangeRequest request)
    {
        var result = await _profileService.ConfirmEmailChangeAsync(request.Token);

        return result.IsSuccess
            ? Ok()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    // The same ceiling the apply form enforces: a CV that may be submitted from here has to be
    // acceptable there too.
    private const long MaxCvSizeBytes = 10 * 1024 * 1024;

    // Rate limited like the apply endpoint: this one accepts 10 MB per request and writes to object
    // storage, so an authenticated caller looping on it costs real bandwidth and real space.
    [HttpPost("cv")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    [RequestSizeLimit(MaxCvSizeBytes + 1024 * 1024)] // file + multipart/form-field overhead
    public async Task<IActionResult> UploadCv(IFormFile file, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(ToProblem(FileSignatureValidator.Empty));

        // Validated at the boundary, before a byte reaches storage: the real leading bytes decide
        // the format, never the extension or the client's declared content type.
        await using var content = file.OpenReadStream();
        var validation = await FileSignatureValidator.ValidateAsync(
            content, file.ContentType, file.Length, MaxCvSizeBytes, ct);
        if (validation.IsFailure)
            return BadRequest(ToProblem(validation.Error));

        var command = new UploadCandidateCvCommand(
            content, file.Length, file.ContentType, file.FileName);

        var result = await _profileService.UploadCvAsync(candidateAccountId, command);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(ToProblem(result.Error));
    }

    [HttpDelete("cv")]
    public async Task<IActionResult> RemoveCv()
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var result = await _profileService.RemoveCvAsync(candidateAccountId);

        return result.IsSuccess
            ? NoContent()
            : NotFound(ToProblem(result.Error));
    }

    // Hands back a signed URL rather than the bytes: the file goes straight from storage to the
    // browser, so a CV never streams through the API.
    [HttpGet("cv/download-url")]
    public async Task<IActionResult> GetCvDownloadUrl()
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var result = await _profileService.GetCvDownloadUrlAsync(candidateAccountId);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(ToProblem(result.Error));
    }

    private static object ToProblem(Error error) => new { error.Code, error.Message };
}
