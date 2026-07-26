using Asp.Versioning;
using Ats.Modules.Tenants.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Tenants.Api;

// The caller's tenant members, plus the administrative actions on them. The list is open to every
// authenticated tenant member on purpose: anyone who can view interviews must be able to resolve
// interviewer names. The mutations below are Admin-only. Tenant isolation keeps everything scoped to
// the caller's own organization.
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

    // POST rather than DELETE: this revokes access but keeps the person, because the audit trail and
    // past interviews reference their id. DELETE would advertise a removal that does not happen.
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

    // "Not found" is the only case that is a 404; every other failure is a rule the caller broke with
    // a request that was otherwise well formed (last admin, targeting themselves, already in state).
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
