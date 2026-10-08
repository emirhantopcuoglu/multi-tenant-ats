using Asp.Versioning;
using Ats.Modules.Tenants.Application;
using Ats.Shared.Kernel;
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
    // PreferredLanguage is optional and settles on English when absent — see the note on the
    // candidate register request; an omitted language is not a reason to refuse an account.
    public sealed record AcceptRequest(
        string Token, string Password, string FirstName, string LastName, string? PreferredLanguage);

    [HttpPost]
    [Authorize(Policy = Policies.CanManageUsers)]
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
            request.Token, request.Password, request.FirstName, request.LastName,
            request.PreferredLanguage ?? SupportedLanguages.Default);
        return result.IsSuccess
            ? Ok()
            : BadRequest(new { result.Error.Code, result.Error.Message });
    }
}
