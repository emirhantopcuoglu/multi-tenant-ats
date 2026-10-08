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

    // PreferredLanguage is optional: an omitted or unrecognised value settles on English rather than
    // failing the registration, because a client that never asked for a language has not made a
    // mistake worth refusing a workspace over.
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

        // 204, not the token pair it used to return: the workspace exists but the session waits for the
        // mailed confirmation link. The SPA shows a "check your inbox" screen instead of signing in.
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    // Anonymous: the link is opened from an email client, which carries no session, and possibly on a
    // different device than the one that registered. The token is the credential.
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
    {
        var result = await _authService.ConfirmEmailAsync(
            request.UserId, request.Token, HttpContext.RequestAborted);

        // No tokens in the response, matching reset-password: confirming must not hand a session to
        // whoever presented a token, only mark the address proven.
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    // Always 204, whether or not the address is registered or already confirmed — a distinguishable
    // response would turn this into a directory of who works here. Rate-limited per IP: it sends mail
    // on demand to an address the caller chooses.
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

    public sealed record SetPreferredLanguageRequest(string Language);

    // Under /auth/me because it writes to the caller's own identity, the same subject Me() reads.
    // Anyone signed in may set their own language, so no policy beyond [Authorize].
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
