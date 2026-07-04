using Asp.Versioning;
using Ats.Modules.Tenants.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Tenants.Api;

// The tenant's own public-profile settings (description, website, location) — the data behind the
// public careers page header. Editing is Admin-only: this is company-wide presentation, the same
// trust level as managing members, not something an individual recruiter should change.
[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = Policies.CanManageTenant)]
[Route("api/v{version:apiVersion}/tenant/profile")]
public sealed class TenantProfileController : ControllerBase
{
    private readonly ITenantProfileService _profileService;

    public TenantProfileController(ITenantProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _profileService.GetAsync(HttpContext.RequestAborted);

        // A valid token whose tenant no longer exists is an inconsistent state, mirroring /auth/me.
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateTenantProfileRequest request)
    {
        var result = await _profileService.UpdateAsync(request, HttpContext.RequestAborted);
        if (result.IsSuccess)
            return Ok(result.Value);

        // Not-found is the token/tenant mismatch case; everything else is input validation.
        return result.Error == TenantProfileErrors.TenantNotFound
            ? NotFound(new { result.Error.Code, result.Error.Message })
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }
}
