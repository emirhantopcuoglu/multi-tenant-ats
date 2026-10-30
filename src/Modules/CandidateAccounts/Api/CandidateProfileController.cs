using Asp.Versioning;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ats.Modules.CandidateAccounts.Api;

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

        return result.Error == CandidateProfileErrors.NotFound
            ? NotFound(new { result.Error.Code, result.Error.Message })
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    public sealed record SetPreferredLanguageRequest(string Language);

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

    // Rate limited: with a stolen token this could be used to guess the current password.
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

    // Rate limited for the same reason as the password endpoint.
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

    // Anonymous: the link may be opened on a device with no session.
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

    // Same limit as the apply form, since this CV can be reused there.
    private const long MaxCvSizeBytes = 10 * 1024 * 1024;

    [HttpPost("cv")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    [RequestSizeLimit(MaxCvSizeBytes + 1024 * 1024)] // file + multipart/form-field overhead
    public async Task<IActionResult> UploadCv(IFormFile file, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(ToProblem(FileSignatureValidator.Empty));

        // The file's leading bytes decide the format, not the extension or content type.
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
