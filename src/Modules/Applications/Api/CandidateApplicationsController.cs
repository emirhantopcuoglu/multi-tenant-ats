using Asp.Versioning;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Applications.Api;

// A candidate's own applications across all companies.
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
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(
            new ListCandidateApplicationsQuery(candidateAccountId, page, pageSize));

        return Ok(result.Value);
    }

    // Someone else's application is a 404, so ids can't be probed.
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

    // POST, not DELETE: the application stays in the history. 404 for unknown or foreign ids, 409 if
    // it is already closed.
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

    // Jobs the candidate has an active application for, so the UI can show "applied".
    [HttpGet("job-ids")]
    public async Task<IActionResult> ListAppliedJobIds()
    {
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new ListCandidateAppliedJobIdsQuery(candidateAccountId));

        return Ok(result.Value);
    }
}
