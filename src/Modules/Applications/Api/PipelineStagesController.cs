using Asp.Versioning;
using Ats.Modules.Applications.Application.Applications;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Applications.Api;

// Pipelines live in the Applications module (a job's stages are an Applications concern), but the
// resource reads naturally as "the stages of a job", so the route is nested under jobs. The route is
// just a string — there is no code dependency on the Jobs module. Same view policy as the rest of
// the recruiter application views.
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/jobs/{jobId:guid}/stages")]
[ApiVersion("1.0")]
public sealed class PipelineStagesController : ControllerBase
{
    private readonly ISender _sender;

    public PipelineStagesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize(Policy = Policies.CanViewApplications)]
    public async Task<IActionResult> List(Guid jobId)
    {
        var result = await _sender.Send(new ListPipelineStagesQuery(jobId));
        return Ok(result.Value);
    }
}
