using System.Security.Claims;
using Asp.Versioning;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Interviews.Api;

// Resolves a room token to the interview behind it, for whichever of the two participant kinds is
// calling — a candidate (their own application) or a company user (an assigned interviewer). Plain
// [Authorize]: no role or CandidateOnly policy, since either token kind is legitimate here and the
// handler tells them apart via the token_type claim and does the real (resource-based) check.
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/interview-room")]
[ApiVersion("1.0")]
public sealed class InterviewRoomController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentTenant _currentTenant;

    public InterviewRoomController(ISender sender, ICurrentTenant currentTenant)
    {
        _sender = sender;
        _currentTenant = currentTenant;
    }

    [HttpGet("{roomToken}")]
    public async Task<IActionResult> Join(string roomToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isCandidate = User.HasClaim(TokenTypes.ClaimName, TokenTypes.Candidate);

        var query = isCandidate
            ? new JoinInterviewRoomQuery(roomToken, userId, CompanyUserId: null, CompanyTenantId: null)
            : new JoinInterviewRoomQuery(roomToken, CandidateAccountId: null, userId, _currentTenant.TenantId);

        var result = await _sender.Send(query);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { result.Error.Code, result.Error.Message });
    }
}
