using Asp.Versioning;
using Ats.Modules.Applications.Application.Applications;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Applications.Api;

// Authenticated recruiter view over applications. Tenant isolation is automatic: every query
// runs through the global query filter, so a recruiter only ever sees their own tenant's data.
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/applications")]
[ApiVersion("1.0")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly ISender _sender;

    public ApplicationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize(Policy = Policies.CanViewApplications)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? jobId = null, [FromQuery] Guid? stageId = null,
        [FromQuery] string? status = null, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _sender.Send(
            new ListApplicationsQuery(jobId, stageId, status, search, page, pageSize));
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanViewApplications)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _sender.Send(new GetApplicationByIdQuery(id));
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    [HttpGet("{id:guid}/cv-download-url")]
    [Authorize(Policy = Policies.CanViewApplications)]
    public async Task<IActionResult> GetCvDownloadUrl(Guid id)
    {
        var result = await _sender.Send(new GetCvDownloadUrlQuery(id));
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    [HttpGet("{id:guid}/activities")]
    [Authorize(Policy = Policies.CanViewApplications)]
    public async Task<IActionResult> GetActivities(Guid id)
    {
        var result = await _sender.Send(new GetApplicationActivityQuery(id));
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    [HttpGet("{id:guid}/cv-parse-result")]
    [Authorize(Policy = Policies.CanViewApplications)]
    public async Task<IActionResult> GetCvParseResult(Guid id)
    {
        // Both failures are 404: the application does not exist in this tenant, or it exists but its
        // CV has not been parsed yet (parsing is asynchronous). The error code distinguishes them.
        var result = await _sender.Send(new GetCvParseResultQuery(id));
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost("{id:guid}/move-stage")]
    [Authorize(Policy = Policies.CanManageApplications)]
    public async Task<IActionResult> MoveStage(Guid id, MoveStageBody body)
    {
        var result = await _sender.Send(new MoveApplicationStageCommand(id, body.TargetStageId));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Policies.CanManageApplications)]
    public async Task<IActionResult> Reject(Guid id, RejectBody body)
    {
        var result = await _sender.Send(new RejectApplicationCommand(id, body.Reason));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    private IActionResult MapFailure(Error error) => error.Code switch
    {
        "application.not_found" => NotFound(new { error.Code, error.Message }),
        _ => BadRequest(new { error.Code, error.Message })
    };

    public sealed record MoveStageBody(Guid TargetStageId);
    public sealed record RejectBody(string Reason);
}
