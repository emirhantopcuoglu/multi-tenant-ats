using Asp.Versioning;
using Ats.Modules.Applications.Application.Applications;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Applications.Api;

// Every interview across the candidate's applications, at every company.
[ApiController]
[Authorize(Policy = Policies.CandidateOnly)]
[Route("api/v{version:apiVersion}/candidate/interviews")]
[ApiVersion("1.0")]
public sealed class CandidateInterviewsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public CandidateInterviewsController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new ListCandidateInterviewsQuery(candidateAccountId));

        return Ok(result.Value);
    }
}
