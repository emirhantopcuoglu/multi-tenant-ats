using System.Security.Claims;
using Asp.Versioning;
using Ats.Modules.Jobs.Application.Jobs;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Jobs.Api;

[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/jobs")]
[ApiVersion("1.0")]
public sealed class JobsController : ControllerBase
{
    private readonly ISender _sender;

    public JobsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanManageJobs)]
    public async Task<IActionResult> Create(CreateJobBody body)
    {
        // The author is the authenticated caller, never a client-supplied value.
        // The JWT 'sub' claim is mapped to NameIdentifier by the default inbound
        // claim mapping, so we read it from there.
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var createdBy))
            return Unauthorized();

        var command = new CreateJobCommand(
            body.Title, body.Description, body.Department, body.Location,
            body.EmploymentType, body.ExperienceLevel,
            body.SalaryMin, body.SalaryMax, body.SalaryCurrency, createdBy);

        var result = await _sender.Send(command);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value })
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanViewJobs)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _sender.Send(new GetJobByIdQuery(id));
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    [HttpGet]
    [Authorize(Policy = Policies.CanViewJobs)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] string? search = null)
    {
        var result = await _sender.Send(new ListJobsQuery(page, pageSize, status, search));
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = Policies.CanManageJobs)]
    public async Task<IActionResult> Publish(Guid id)
    {
        var result = await _sender.Send(new PublishJobCommand(id));
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = Policies.CanManageJobs)]
    public async Task<IActionResult> Close(Guid id)
    {
        var result = await _sender.Send(new CloseJobCommand(id));
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = Policies.CanManageJobs)]
    public async Task<IActionResult> Archive(Guid id)
    {
        var result = await _sender.Send(new ArchiveJobCommand(id));
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManageJobs)]
    public async Task<IActionResult> Update(Guid id, UpdateJobBody body)
    {
        var command = new UpdateJobCommand(
            id, body.Title, body.Description, body.Department, body.Location,
            body.EmploymentType, body.ExperienceLevel, body.SalaryMin, body.SalaryMax, body.SalaryCurrency);

        var result = await _sender.Send(command);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    // Request shape for creation: deliberately omits CreatedBy so the client
    // cannot spoof authorship. The controller fills it from the JWT.
    public sealed record CreateJobBody(
        string Title, string Description, string Department, string Location,
        Ats.Modules.Jobs.Domain.EmploymentType EmploymentType,
        Ats.Modules.Jobs.Domain.ExperienceLevel ExperienceLevel,
        decimal? SalaryMin, decimal? SalaryMax, string? SalaryCurrency);

    public sealed record UpdateJobBody(
        string Title, string Description, string Department, string Location,
        Ats.Modules.Jobs.Domain.EmploymentType EmploymentType,
        Ats.Modules.Jobs.Domain.ExperienceLevel ExperienceLevel,
        decimal? SalaryMin, decimal? SalaryMax, string? SalaryCurrency);
}
