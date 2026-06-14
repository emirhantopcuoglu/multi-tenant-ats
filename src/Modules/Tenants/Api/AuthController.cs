using Asp.Versioning;
using Ats.Modules.Tenants.Application;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Tenants.Api;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    public sealed record RegisterRequest(
        string CompanyName, string Slug, string Email, string Password, string FirstName, string LastName);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record RefreshRequest(string RefreshToken);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(
            request.CompanyName, request.Slug, request.Email, request.Password, request.FirstName, request.LastName);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost("login")]
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
}
