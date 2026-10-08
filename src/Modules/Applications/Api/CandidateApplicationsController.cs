using Asp.Versioning;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Applications.Api;

// Candidate-facing view of their own applications across all companies. The CandidateOnly
// policy ensures this is only reachable with a candidate JWT — a company token cannot satisfy
// the token_type=candidate claim requirement.
[ApiController]
[Authorize(Policy = Policies.CandidateOnly)]
[Route("api/v{version:apiVersion}/candidate/applications")]
[ApiVersion("1.0")]
public sealed class CandidateApplicationsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public CandidateApplicationsController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        // CandidateOnly policy guarantees the token is present and carries the sub claim.
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(
            new ListCandidateApplicationsQuery(candidateAccountId, page, pageSize));

        return Ok(result.Value);
    }

    // The transparent tracking view: full pipeline + timeline for one of the candidate's own
    // applications. An id that exists but belongs to another candidate is a 404, not a 403 —
    // the handler folds ownership into the lookup so ids cannot be probed for existence.
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(
            new GetCandidateApplicationDetailQuery(candidateAccountId, id));

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    // The candidate closes their own application. POST rather than DELETE: the application is not
    // removed, it reaches a terminal status and stays fully visible in their history.
    //
    // The two failures map differently on purpose — 404 means "no such application of yours" (an
    // unknown or foreign id, indistinguishable by design), 409 means "yours, but already closed",
    // which is a stale tab rather than anything suspicious.
    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id)
    {
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new WithdrawApplicationCommand(candidateAccountId, id));
        if (result.IsSuccess)
            return NoContent();

        return result.Error.Code == ApplicationErrors.NotFound.Code
            ? NotFound(new { result.Error.Code, result.Error.Message })
            : Conflict(new { result.Error.Code, result.Error.Message });
    }

    // Membership set for the public job pages: which jobs does this candidate currently have an
    // Active application for? Lets the UI swap the apply CTA for an "already applied" state.
    [HttpGet("job-ids")]
    public async Task<IActionResult> ListAppliedJobIds()
    {
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new ListCandidateAppliedJobIdsQuery(candidateAccountId));

        return Ok(result.Value);
    }
}
