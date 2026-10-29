using Asp.Versioning;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ats.Modules.CandidateAccounts.Api;

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

    // Freezing keeps the session valid, so a frozen candidate can call this themselves.
    [HttpPost("reactivate")]
    public async Task<IActionResult> Reactivate()
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var result = await _lifecycleService.ReactivateAsync(candidateAccountId);
        return ToResponse(result);
    }

    public sealed record DeleteCandidateAccountRequest(string CurrentPassword);

    // Rate limited: with a stolen token this could be used to guess the current password.
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
