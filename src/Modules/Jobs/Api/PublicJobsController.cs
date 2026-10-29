using Ats.Modules.Jobs.Application.Jobs;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Jobs.Api;

// Public job board. The tenant comes from the path slug via TenantResolutionMiddleware.
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

    // slug is only bound so the route matches; the middleware already resolved the tenant.
    [HttpGet]
    public async Task<IActionResult> List(
        string slug, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        // An unknown slug is a 404, not an empty 200.
        if (!_currentTenant.TenantId.HasValue)
            return NotFound();

        var result = await _sender.Send(new ListPublicJobsQuery(page, pageSize));
        return Ok(result.Value);
    }

    // Drafts, closed jobs and unknown slugs are all a 404, so nothing leaks.
    [HttpGet("{jobSlug}")]
    public async Task<IActionResult> Detail(string slug, string jobSlug)
    {
        if (!_currentTenant.TenantId.HasValue)
            return NotFound();

        var result = await _sender.Send(new GetPublicJobBySlugQuery(jobSlug));
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
}
