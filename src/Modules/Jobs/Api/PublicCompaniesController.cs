using Ats.Modules.Jobs.Application.Jobs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Jobs.Api;

// Public directory of companies with published jobs, across all tenants. Lives in Jobs because the
// list and counts come from job data; names come from the Tenants module through a port.
[ApiController]
[AllowAnonymous]
[Route("public/companies")]
public sealed class PublicCompaniesController : ControllerBase
{
    private readonly ISender _sender;

    public PublicCompaniesController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _sender.Send(new ListPublicCompaniesQuery(page, pageSize, search));
        return Ok(result.Value);
    }

    // An unknown slug is a 404, not an empty 200.
    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        var result = await _sender.Send(new GetPublicCompanyBySlugQuery(slug));
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
}
