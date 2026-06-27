using Asp.Versioning;
using Ats.Modules.Applications.Application.Applications;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Applications.Api;

[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/candidates")]
[ApiVersion("1.0")]
public sealed class CandidatesController : ControllerBase
{
    private readonly ISender _sender;

    public CandidatesController(ISender sender) => _sender = sender;

    [HttpGet]
    [Authorize(Policy = Policies.CanViewApplications)]
    public async Task<IActionResult> Search(
        [FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _sender.Send(new SearchCandidatesQuery(q, page, pageSize));
        return Ok(result.Value);
    }
}
