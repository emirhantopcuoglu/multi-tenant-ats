using Asp.Versioning;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Tenants.Api;

[ApiController]
[Route("api/v{version:apiVersion}/invitations")]
[ApiVersion("1.0")]
public sealed class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitations;

    public InvitationsController(IInvitationService invitations)
    {
        _invitations = invitations;
    }

    public sealed record InviteRequest(string Email, string Role);
    public sealed record AcceptRequest(string Token, string Password, string FirstName, string LastName);

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Invite(InviteRequest request)
    {
        var result = await _invitations.InviteAsync(request.Email, request.Role);
        return result.IsSuccess
            ? Ok()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> Accept(AcceptRequest request)
    {
        var result = await _invitations.AcceptAsync(
            request.Token, request.Password, request.FirstName, request.LastName);
        return result.IsSuccess
            ? Ok()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }
}
