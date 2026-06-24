using Asp.Versioning;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Modules.Interviews.Domain;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Interviews.Api;

// Authenticated recruiter/hiring-manager view over interviews. Tenant isolation is automatic: every
// query runs through the global query filter, so a caller only ever sees their own tenant's data.
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/interviews")]
[ApiVersion("1.0")]
public sealed class InterviewsController : ControllerBase
{
    private readonly ISender _sender;

    public InterviewsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize(Policy = Policies.CanViewInterviews)]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? interviewerId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _sender.Send(
            new ListInterviewsQuery(fromDate, toDate, interviewerId, page, pageSize));
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanViewInterviews)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _sender.Send(new GetInterviewByIdQuery(id));
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanManageInterviews)]
    public async Task<IActionResult> Schedule(ScheduleInterviewBody body)
    {
        var command = new ScheduleInterviewCommand(
            body.ApplicationId, body.Type, body.ScheduledAtUtc, body.DurationMinutes,
            body.Location, body.InterviewerUserIds ?? [], body.Notes);

        var result = await _sender.Send(command);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value })
            : MapFailure(result.Error);
    }

    [HttpPut("{id:guid}/reschedule")]
    [Authorize(Policy = Policies.CanManageInterviews)]
    public async Task<IActionResult> Reschedule(Guid id, RescheduleBody body)
    {
        var result = await _sender.Send(
            new RescheduleInterviewCommand(id, body.ScheduledAtUtc, body.DurationMinutes));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.CanManageInterviews)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var result = await _sender.Send(new CancelInterviewCommand(id));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Policies.CanManageInterviews)]
    public async Task<IActionResult> Complete(Guid id)
    {
        var result = await _sender.Send(new CompleteInterviewCommand(id));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    [HttpPost("{id:guid}/no-show")]
    [Authorize(Policy = Policies.CanManageInterviews)]
    public async Task<IActionResult> MarkNoShow(Guid id)
    {
        var result = await _sender.Send(new MarkInterviewNoShowCommand(id));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    private IActionResult MapFailure(Error error) => error.Code switch
    {
        "interview.application_not_found" => NotFound(new { error.Code, error.Message }),
        "interview.not_found" => NotFound(new { error.Code, error.Message }),
        _ => BadRequest(new { error.Code, error.Message })
    };

    public sealed record ScheduleInterviewBody(
        Guid ApplicationId,
        InterviewType Type,
        DateTime ScheduledAtUtc,
        int DurationMinutes,
        string? Location,
        IReadOnlyList<Guid>? InterviewerUserIds,
        string? Notes);

    public sealed record RescheduleBody(DateTime ScheduledAtUtc, int DurationMinutes);
}
