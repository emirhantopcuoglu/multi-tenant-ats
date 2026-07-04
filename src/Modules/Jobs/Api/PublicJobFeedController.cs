using Ats.Modules.Jobs.Application.Jobs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Jobs.Api;

// The cross-tenant public marketplace feed. Unlike PublicJobsController (/{slug}/jobs), this is NOT
// scoped to one company: it lists every tenant's Published jobs. So its route is the literal prefix
// "public/jobs" with no {slug} segment and no tenant to resolve — a literal route segment outranks
// the {slug} parameter of PublicJobsController, so the two never collide. No api/v{version} prefix:
// like the careers pages, this is a user-facing URL, and "public" is a reserved slug so no company
// can ever shadow it.
[ApiController]
[AllowAnonymous]
[Route("public/jobs")]
public sealed class PublicJobFeedController : ControllerBase
{
    private readonly ISender _sender;

    public PublicJobFeedController(ISender sender) => _sender = sender;

    // The filters arrive as raw strings and stay strings all the way to the handler, which treats
    // anything unparseable as "no filter" — a shared marketplace URL with a stale or mistyped value
    // should render the unfiltered list, not a 400.
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] string? employmentType = null,
        [FromQuery] string? experienceLevel = null,
        [FromQuery] string? location = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _sender.Send(new ListPublicJobFeedQuery(
            page, pageSize, search, employmentType, experienceLevel, location));
        return Ok(result.Value);
    }
}
