using Asp.Versioning;
using Ats.Modules.Tenants.Application;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Tenants.Api;

// Admin-only: this is the company's public profile.
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

        return result.Error == TenantProfileErrors.TenantNotFound
            ? NotFound(new { result.Error.Code, result.Error.Message })
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }
}
