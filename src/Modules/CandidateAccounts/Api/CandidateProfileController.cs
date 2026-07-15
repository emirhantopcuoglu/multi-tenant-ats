using Asp.Versioning;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
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
}
