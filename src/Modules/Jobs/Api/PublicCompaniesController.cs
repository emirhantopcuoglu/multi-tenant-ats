using Ats.Modules.Jobs.Application.Jobs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Jobs.Api;

// The public directory of hiring companies, sibling to the cross-tenant job feed. Like it, the route
// is the literal prefix "public/companies" (no {slug} to resolve, "public" is a reserved slug), and
// the data spans all tenants. It lives in the Jobs module because a "company" here means "a tenant
// with Published jobs" — the list and the open-role counts are derived from this module's data, with
// the company name/slug enriched from the Tenants module through a port.
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

    // A single company's public profile. An unknown slug reads as 404 rather than an empty 200, so the
    // careers page can surface a genuine "no such company" state instead of a blank header.
    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        var result = await _sender.Send(new GetPublicCompanyBySlugQuery(slug));
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
}
