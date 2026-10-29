using System.Security.Claims;
using Asp.Versioning;
using Ats.Modules.Interviews.Api.Authorization;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Modules.Interviews.Domain;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Interviews.Api;

[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/interviews")]
[ApiVersion("1.0")]
public sealed class InterviewsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IAuthorizationService _authorizationService;

    public InterviewsController(ISender sender, IAuthorizationService authorizationService)
    {
        _sender = sender;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    [Authorize(Policy = Policies.CanViewInterviews)]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? interviewerId = null, [FromQuery] Guid? applicationId = null,
        [FromQuery] InterviewListFilter? filter = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _sender.Send(
            new ListInterviewsQuery(fromDate, toDate, interviewerId, applicationId, filter, page, pageSize));
        return Ok(result.Value);
    }

    // Feedback summary across all of an application's interviews.
    [HttpGet("outcome")]
    [Authorize(Policy = Policies.CanViewInterviews)]
    public async Task<IActionResult> GetApplicationOutcome([FromQuery] Guid applicationId)
    {
        var result = await _sender.Send(new GetApplicationInterviewOutcomeQuery(applicationId));
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
            body.InterviewerUserIds ?? [], body.Notes);

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
    public async Task<IActionResult> Cancel(Guid id, CancelInterviewBody body)
    {
        var result = await _sender.Send(new CancelInterviewCommand(id, body.Reason, body.Note));
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
    public async Task<IActionResult> MarkNoShow(Guid id, MarkNoShowBody body)
    {
        var result = await _sender.Send(new MarkInterviewNoShowCommand(id, body.Party));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    [HttpPut("{id:guid}/interviewers")]
    [Authorize(Policy = Policies.CanManageInterviews)]
    public async Task<IActionResult> ReassignInterviewers(Guid id, ReassignInterviewersBody body)
    {
        var result = await _sender.Send(
            new ReassignInterviewersCommand(id, body.InterviewerUserIds ?? []));
        return result.IsSuccess ? NoContent() : MapFailure(result.Error);
    }

    [HttpGet("{id:guid}/feedback")]
    [Authorize(Policy = Policies.CanViewInterviews)]
    public async Task<IActionResult> GetFeedback(Guid id)
    {
        // The caller decides whether the panel's feedback is withheld; see GetInterviewFeedbackHandler.
        var result = await _sender.Send(new GetInterviewFeedbackQuery(id, CurrentUserId()));
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    // CanViewInterviews, not CanManageInterviews: an interviewer isn't necessarily a recruiter.
    // IsInterviewParticipant is the real check.
    [HttpPost("{id:guid}/feedback")]
    [Authorize(Policy = Policies.CanViewInterviews)]
    public async Task<IActionResult> SubmitFeedback(Guid id, SubmitFeedbackBody body)
    {
        var interviewResult = await _sender.Send(new GetInterviewByIdQuery(id));
        if (!interviewResult.IsSuccess)
            return MapFailure(interviewResult.Error);

        // Only an interviewer on this panel may submit feedback.
        var authResult = await _authorizationService.AuthorizeAsync(
            User, interviewResult.Value, Policies.IsInterviewParticipant);
        if (!authResult.Succeeded)
            return Forbid();

        // The interviewer comes from the token, not the request body.
        var command = new SubmitInterviewFeedbackCommand(
            id, CurrentUserId(), body.Rating, body.Recommendation, body.Comments);

        var result = await _sender.Send(command);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id }, new { id = result.Value })
            : MapFailure(result.Error);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private IActionResult MapFailure(Error error) => error.Code switch
    {
        "interview.application_not_found" => NotFound(new { error.Code, error.Message }),
        "interview.not_found" => NotFound(new { error.Code, error.Message }),
        "interview.transition_not_allowed" => Conflict(new { error.Code, error.Message }),
        "interview.feedback_not_eligible" => Conflict(new { error.Code, error.Message }),
        "interview.duplicate_feedback" => Conflict(new { error.Code, error.Message }),
        "interview.interviewer_conflict" => Conflict(new { error.Code, error.Message }),
        "interview.candidate_conflict" => Conflict(new { error.Code, error.Message }),
        _ => BadRequest(new { error.Code, error.Message })
    };

    public sealed record ScheduleInterviewBody(
        Guid ApplicationId,
        InterviewType Type,
        DateTime ScheduledAtUtc,
        int DurationMinutes,
        IReadOnlyList<Guid>? InterviewerUserIds,
        string? Notes);

    public sealed record RescheduleBody(DateTime ScheduledAtUtc, int DurationMinutes);

    // Note stays internal; only Reason reaches the candidate.
    public sealed record CancelInterviewBody(InterviewCancellationReason Reason, string? Note);

    public sealed record MarkNoShowBody(NoShowParty Party);

    // The full new panel, not a diff, so concurrent edits can't half-apply.
    public sealed record ReassignInterviewersBody(IReadOnlyList<Guid>? InterviewerUserIds);

    public sealed record SubmitFeedbackBody(
        int Rating,
        FeedbackRecommendation Recommendation,
        string? Comments);
}
