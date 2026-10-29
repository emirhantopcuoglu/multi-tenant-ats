using Asp.Versioning;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ats.Modules.CandidateAccounts.Api;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/candidate/auth")]
public sealed class CandidateAuthController : ControllerBase
{
    private readonly ICandidateAuthService _authService;
    private readonly ICandidatePasswordResetService _passwordResetService;
    private readonly ICandidateEmailVerificationService _emailVerificationService;
    private readonly ICurrentUser _currentUser;

    public CandidateAuthController(
        ICandidateAuthService authService,
        ICandidatePasswordResetService passwordResetService,
        ICandidateEmailVerificationService emailVerificationService,
        ICurrentUser currentUser)
    {
        _authService = authService;
        _passwordResetService = passwordResetService;
        _emailVerificationService = emailVerificationService;
        _currentUser = currentUser;
    }

    // A missing or unknown PreferredLanguage falls back to English.
    public sealed record RegisterRequest(
        string Email, string Password, string FirstName, string LastName, string? PreferredLanguage);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record LogoutRequest(string RefreshToken);
    public sealed record ForgotPasswordRequest(string Email);
    public sealed record ResetPasswordRequest(string Token, string NewPassword);
    public sealed record VerifyEmailRequest(string Token);

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(
            request.Email, request.Password, request.FirstName, request.LastName, request.PreferredLanguage
            ?? SupportedLanguages.Default);

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
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var result = await _authService.RefreshAsync(request.RefreshToken);

        // 401 so the client drops the session and signs in again.
        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutRequest request)
    {
        await _authService.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    // Always 204, so it can't be used to check which emails are registered.
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _passwordResetService.RequestAsync(request.Email, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await _passwordResetService.ResetAsync(
            request.Token, request.NewPassword, HttpContext.RequestAborted);

        // No session is returned; the user signs in with the new password.
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    // Anonymous: the link may be opened on a device with no session.
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var result = await _emailVerificationService.ConfirmAsync(
            request.Token, HttpContext.RequestAborted);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost("resend-verification")]
    [Authorize(Policy = Policies.CandidateOnly)]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ResendVerification()
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var result = await _emailVerificationService.SendAsync(
            candidateAccountId, HttpContext.RequestAborted);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    [HttpGet("me")]
    [Authorize(Policy = Policies.CandidateOnly)]
    public async Task<IActionResult> Me()
    {
        if (_currentUser.UserId is not { } candidateAccountId)
            return Unauthorized();

        var result = await _authService.GetCurrentCandidateAsync(candidateAccountId);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }
}
