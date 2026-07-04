using Asp.Versioning;
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
