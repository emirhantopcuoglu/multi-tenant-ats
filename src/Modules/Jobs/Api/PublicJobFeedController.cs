using Ats.Modules.Jobs.Application.Jobs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Jobs.Api;

// Published jobs from every tenant. The literal "public" segment outranks the {slug} route, and
// "public" is a reserved slug.
[ApiController]
[AllowAnonymous]
[Route("public/jobs")]
public sealed class PublicJobFeedController : ControllerBase
{
    private readonly ISender _sender;

    public PublicJobFeedController(ISender sender) => _sender = sender;

    // Unparseable filter values are ignored rather than rejected, so a stale shared URL still works.
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] string? employmentType = null,
        [FromQuery] string? experienceLevel = null,
        [FromQuery] string? workArrangement = null,
        [FromQuery] string? location = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _sender.Send(new ListPublicJobFeedQuery(
            page, pageSize, search, employmentType, experienceLevel, workArrangement, location));
        return Ok(result.Value);
    }
}
