using System.Security.Claims;
using Asp.Versioning;
using Ats.Modules.Interviews.Application.Interviews;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Interviews.Api;

// Open to both candidate and company tokens. The handler tells them apart by token_type and checks
// that the caller is a participant.
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
