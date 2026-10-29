using Asp.Versioning;
using Ats.Modules.Tenants.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Tenants.Api;

// Listing is open to every member so interviewer names can be resolved. Changes are Admin-only.
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/users")]
[ApiVersion("1.0")]
public sealed class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserManagementService _userManagement;

    public UsersController(IAuthService authService, IUserManagementService userManagement)
    {
        _authService = authService;
        _userManagement = userManagement;
    }

    public sealed record ChangeRoleRequest(string Role);

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var users = await _authService.ListTenantUsersAsync();
        return Ok(users);
    }

    [HttpPut("{id:guid}/role")]
    [Authorize(Policy = Policies.CanManageUsers)]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeRoleRequest request)
    {
        var result = await _userManagement.ChangeRoleAsync(id, request.Role, HttpContext.RequestAborted);
        return ToResponse(result);
    }

    // POST, not DELETE: the user is kept because past interviews and audit fields reference them.
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Policies.CanManageUsers)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var result = await _userManagement.DeactivateAsync(id, HttpContext.RequestAborted);
        return ToResponse(result);
    }

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Policy = Policies.CanManageUsers)]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var result = await _userManagement.ReactivateAsync(id, HttpContext.RequestAborted);
        return ToResponse(result);
    }

    private IActionResult ToResponse(Result result)
    {
        if (result.IsSuccess)
            return NoContent();

        var payload = new { result.Error.Code, result.Error.Message };
        return result.Error.Code == UserManagementErrors.NotFound.Code
            ? NotFound(payload)
            : BadRequest(payload);
    }
}
