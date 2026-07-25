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

// Authenticated recruiter/hiring-manager view over interviews. Tenant isolation is automatic: every
// query runs through the global query filter, so a caller only ever sees their own tenant's data.
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
        // The caller's identity decides whether the panel's evaluations are withheld, so it comes
        // from the JWT rather than the route — see GetInterviewFeedbackHandler.
        var result = await _sender.Send(new GetInterviewFeedbackQuery(id, CurrentUserId()));
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    // Gated on viewing rather than managing interviews: submitting feedback is what an assigned
    // interviewer does, and an interviewer is not necessarily a recruiter. CanManageInterviews
    // excluded the ReadOnly role, so a ReadOnly user put on a panel could not evaluate the candidate
    // they had just interviewed. IsInterviewParticipant below is the real gate — it always was.
    [HttpPost("{id:guid}/feedback")]
    [Authorize(Policy = Policies.CanViewInterviews)]
    public async Task<IActionResult> SubmitFeedback(Guid id, SubmitFeedbackBody body)
    {
        // Load the interview first so we can run a resource-based authorization check.
        var interviewResult = await _sender.Send(new GetInterviewByIdQuery(id));
        if (!interviewResult.IsSuccess)
            return MapFailure(interviewResult.Error);

        // Second gate: only an assigned interviewer may submit feedback for this specific interview.
        // CanManageInterviews (above) checks the role; IsInterviewParticipant checks the resource.
        var authResult = await _authorizationService.AuthorizeAsync(
            User, interviewResult.Value, Policies.IsInterviewParticipant);
        if (!authResult.Succeeded)
            return Forbid();

        // The interviewer's identity comes from the JWT, not from the request body — callers must
        // not be able to self-declare another user's identity.
        var command = new SubmitInterviewFeedbackCommand(
            id, CurrentUserId(), body.Rating, body.Recommendation, body.Comments);

        var result = await _sender.Send(command);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id }, new { id = result.Value })
            : MapFailure(result.Error);
    }

    // Always present: every action here sits behind [Authorize], so the subject claim exists.
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

    // Note is the recruiter's internal wording and never reaches the candidate — only Reason does,
    // and only as the sentence the cancellation email leads with.
    public sealed record CancelInterviewBody(InterviewCancellationReason Reason, string? Note);

    public sealed record MarkNoShowBody(NoShowParty Party);

    // The full replacement panel, not a delta: a caller sending the list it wants cannot race a
    // concurrent edit into a half-applied add/remove pair.
    public sealed record ReassignInterviewersBody(IReadOnlyList<Guid>? InterviewerUserIds);

    public sealed record SubmitFeedbackBody(
        int Rating,
        FeedbackRecommendation Recommendation,
        string? Comments);
}
