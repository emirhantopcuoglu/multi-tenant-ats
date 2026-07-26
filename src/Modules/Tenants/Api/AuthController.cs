using Asp.Versioning;
using Ats.Modules.Tenants.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ats.Modules.Tenants.Api;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUser _currentUser;

    public AuthController(IAuthService authService, ICurrentUser currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    public sealed record RegisterRequest(
        string CompanyName, string Slug, string Email, string Password, string FirstName, string LastName);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record ForgotPasswordRequest(string Email);
    public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(
            request.CompanyName, request.Slug, request.Email, request.Password, request.FirstName, request.LastName);

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

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var result = await _authService.RefreshAsync(request.RefreshToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        await _authService.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    // Always 204, whether or not the address is registered: a distinguishable response would let
    // anyone enumerate who works at a company on this platform. Rate-limited because it sends mail.
    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _authService.RequestPasswordResetAsync(request.Email, HttpContext.RequestAborted);
        return NoContent();
    }

    // Anonymous by necessity: whoever needs this cannot sign in. The mailed token is the credential.
    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(
            request.UserId, request.Token, request.NewPassword, HttpContext.RequestAborted);

        // No tokens in the response: whoever set the password signs in with it, so guessing a token
        // cannot hand out a session.
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        // [Authorize] guarantees an authenticated principal, so UserId is present; the guard only
        // covers the impossible case of a token without a 'sub' claim, mapping it to 401.
        if (_currentUser.UserId is not { } userId)
            return Unauthorized();

        var result = await _authService.GetCurrentUserAsync(userId);

        // A valid token whose user/tenant no longer exists is an inconsistent state, not a client
        // error in the request — 404 communicates "this authenticated identity has no profile".
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }
}
