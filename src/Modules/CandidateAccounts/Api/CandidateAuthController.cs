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

    // PreferredLanguage is optional: an omitted or unrecognised value settles on English rather than
    // failing the registration, because a client that never asked for a language has not made a
    // mistake worth refusing an account over.
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

    // Anonymous like the company /auth/refresh: the refresh token IS the credential, and the access
    // token it replaces has expired by definition, so requiring one would make the endpoint useless.
    // Rate-limited per IP because an unauthenticated endpoint that mints sessions is worth guessing at.
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var result = await _authService.RefreshAsync(request.RefreshToken);

        // 401 rather than 400: the client's correct reaction is to drop the session and sign in again,
        // which is what it already does for any other 401.
        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new { result.Error.Code, result.Error.Message });
    }

    // Anonymous for the same reason as refresh, and idempotent: revoking a token that was already
    // dead is still a successful logout, so this never reports failure.
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutRequest request)
    {
        await _authService.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    // Always 204, whether or not the address is registered: a distinguishable response would turn this
    // into a directory of who has an account. Rate-limited per IP because it sends mail on demand.
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _passwordResetService.RequestAsync(request.Email, HttpContext.RequestAborted);
        return NoContent();
    }

    // Anonymous by necessity: the caller cannot sign in, which is why they are here. The mailed token
    // is the credential, and it is single-use and hour-limited.
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await _passwordResetService.ResetAsync(
            request.Token, request.NewPassword, HttpContext.RequestAborted);

        // No tokens in the response on purpose: whoever just set the password signs in with it. That
        // also means a reset cannot hand a session to someone who only guessed at a token.
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    // Anonymous by necessity: the link is clicked from an email client, which carries no session, and
    // the candidate may well be verifying on a different device than the one they registered on. The
    // token itself is the credential — 256 bits, single-use, 24 hours.
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var result = await _emailVerificationService.ConfirmAsync(
            request.Token, HttpContext.RequestAborted);

        // No tokens in the response, matching reset-password: verifying must not hand a session to
        // whoever presented a token, only mark the address proven.
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    // Authenticated, unlike forgot-password: this one only ever mails the address already on the
    // signed-in account, so there is no way to aim it at a stranger and nothing to hide from the
    // caller. Still per-IP limited — it sends mail on demand.
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
