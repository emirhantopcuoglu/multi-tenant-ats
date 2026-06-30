using Asp.Versioning;
using Ats.Modules.Tenants.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Tenants.Api;

// Read-only directory of the caller's tenant members. Authenticated access (not a manage policy) is
// intentional: anyone who can view interviews must be able to resolve interviewer names, so the list
// is open to every tenant member. Tenant isolation keeps it scoped to the caller's own organization.
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/users")]
[ApiVersion("1.0")]
public sealed class UsersController : ControllerBase
{
    private readonly IAuthService _authService;

    public UsersController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var users = await _authService.ListTenantUsersAsync();
        return Ok(users);
    }
}
