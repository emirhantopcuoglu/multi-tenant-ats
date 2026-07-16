using Asp.Versioning;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ats.Modules.CandidateAccounts.Api;

// The account itself as a resource — distinct from /candidate/profile, which edits what the account
// says about its owner. This controller changes whether the account is usable at all, which is why
// it gets its own surface instead of more actions on the profile controller.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/candidate/account")]
[Authorize(Policy = Policies.CandidateOnly)]
public sealed class CandidateAccountController : ControllerBase
{
    private readonly ICandidateAccountLifecycleService _lifecycleService;
    private readonly ICurrentUser _currentUser;

    public CandidateAccountController(
        ICandidateAccountLifecycleService lifecycleService, ICurrentUser currentUser)
    {
        _lifecycleService = lifecycleService;
        _currentUser = currentUser;
    }

    [HttpPost("freeze")]
    public async Task<IActionResult> Freeze()
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var result = await _lifecycleService.FreezeAsync(candidateAccountId);
        return ToResponse(result);
    }

    // Reachable by a frozen account on purpose: freezing does not rotate the security stamp, so the
    // frozen candidate's own session stays valid and can undo the freeze from the reactivation
    // screen without support intervention.
    [HttpPost("reactivate")]
    public async Task<IActionResult> Reactivate()
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var result = await _lifecycleService.ReactivateAsync(candidateAccountId);
        return ToResponse(result);
    }

    public sealed record DeleteCandidateAccountRequest(string CurrentPassword);

    // Rate limited like every endpoint that verifies the current password: without it, a stolen
    // token could brute-force the password against this check.
    [HttpDelete]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> Delete(DeleteCandidateAccountRequest request)
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var command = new DeleteCandidateAccountCommand(request.CurrentPassword);
        var result = await _lifecycleService.DeleteAsync(candidateAccountId, command);
        return ToResponse(result);
    }

    private IActionResult ToResponse(Result result)
    {
        if (result.IsSuccess)
            return Ok();

        return result.Error == CandidateAccountLifecycleErrors.NotFound
            ? NotFound(new { result.Error.Code, result.Error.Message })
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }
}
