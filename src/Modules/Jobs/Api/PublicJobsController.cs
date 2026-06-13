using Ats.Modules.Jobs.Application.Jobs;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Jobs.Api;

// Public, unauthenticated job board. The tenant comes from the path slug
// (e.g. /acmecorp/jobs), resolved by TenantResolutionMiddleware before the
// request reaches here. No api/v{version} prefix: this is a user-facing URL.
[ApiController]
[AllowAnonymous]
[Route("{slug}/jobs")]
public sealed class PublicJobsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentTenant _currentTenant;

    public PublicJobsController(ISender sender, ICurrentTenant currentTenant)
    {
        _sender = sender;
        _currentTenant = currentTenant;
    }

    // `slug` is bound only so the route matches; the tenant it identifies has
    // already been resolved into the tenant context by the middleware.
    [HttpGet]
    public async Task<IActionResult> List(
        string slug, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        // An unknown slug leaves the tenant unresolved. Surface that as 404 rather
        // than a misleading empty 200 — the company page genuinely does not exist.
        if (!_currentTenant.TenantId.HasValue)
            return NotFound();

        var result = await _sender.Send(new ListPublicJobsQuery(page, pageSize));
        return Ok(result.Value);
    }
}
