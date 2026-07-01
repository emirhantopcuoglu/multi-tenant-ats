using Asp.Versioning;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ats.Modules.CandidateAccounts.Api;

// Candidate-side authentication, parallel to the company AuthController but for the global marketplace
// identity. register/login are anonymous and share the per-IP limiter that guards the company auth
// endpoints; me requires a candidate token through the CandidateOnly policy, which a company token
// cannot satisfy (it carries no token_type=candidate claim).
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/candidate/auth")]
public sealed class CandidateAuthController : ControllerBase
{
    private readonly ICandidateAuthService _authService;
    private readonly ICurrentUser _currentUser;

    public CandidateAuthController(ICandidateAuthService authService, ICurrentUser currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName);
    public sealed record LoginRequest(string Email, string Password);

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(
            request.Email, request.Password, request.FirstName, request.LastName);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password);

        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new { result.Error.Code, result.Error.Message });
    }

    [HttpGet("me")]
    [Authorize(Policy = Policies.CandidateOnly)]
    public async Task<IActionResult> Me()
    {
        // The CandidateOnly policy guarantees an authenticated candidate token, so UserId (the 'sub'
        // claim) is present; this guard only covers the impossible token-without-sub case.
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var result = await _authService.GetCurrentCandidateAsync(candidateAccountId);

        // A valid token whose account no longer exists is an inconsistent state, not a bad request.
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }
}
