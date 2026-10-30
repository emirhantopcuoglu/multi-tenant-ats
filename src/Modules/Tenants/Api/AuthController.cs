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

    // A missing or unknown PreferredLanguage falls back to English.
    public sealed record RegisterRequest(
        string CompanyName, string Slug, string Email, string Password, string FirstName, string LastName,
        string? PreferredLanguage);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record ForgotPasswordRequest(string Email);
    public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);
    public sealed record ConfirmEmailRequest(Guid UserId, string Token);
    public sealed record ResendConfirmationRequest(string Email);

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(
            request.CompanyName, request.Slug, request.Email, request.Password, request.FirstName,
            request.LastName, request.PreferredLanguage ?? SupportedLanguages.Default);

        // No session yet: login waits for the confirmation link.
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    // Anonymous: the link may be opened on a device with no session.
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
    {
        var result = await _authService.ConfirmEmailAsync(
            request.UserId, request.Token, HttpContext.RequestAborted);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    // Always 204, so it can't be used to check which emails are registered.
    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request)
    {
        await _authService.ResendEmailConfirmationAsync(request.Email, HttpContext.RequestAborted);
        return NoContent();
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

    // Always 204, so it can't be used to check which emails are registered.
    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _authService.RequestPasswordResetAsync(request.Email, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(
            request.UserId, request.Token, request.NewPassword, HttpContext.RequestAborted);

        // No session is returned; the user signs in with the new password.
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        if (_currentUser.UserId is not { } userId)
            return Unauthorized();

        var result = await _authService.GetCurrentUserAsync(userId);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    public sealed record SetPreferredLanguageRequest(string Language);

    [HttpPut("me/language")]
    [Authorize]
    public async Task<IActionResult> SetPreferredLanguage(SetPreferredLanguageRequest request)
    {
        if (_currentUser.UserId is not { } userId)
            return Unauthorized();

        var result = await _authService.SetPreferredLanguageAsync(userId, request.Language);

        if (result.IsSuccess)
            return NoContent();

        return result.Error == AuthErrors.UserNotFound
            ? NotFound(new { result.Error.Code, result.Error.Message })
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }
}
