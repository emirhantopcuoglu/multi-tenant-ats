using Asp.Versioning;
using Ats.Modules.Applications.Application.Applications;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Applications.Api;

// Pipelines belong to this module; the route is under jobs only because that's how it reads.
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
