using Asp.Versioning;
using Ats.Modules.Notifications.Application.Notifications;
using Ats.Modules.Notifications.Domain;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Notifications.Api;

// The candidate's notification feed: what powers the bell icon. The CandidateOnly policy ensures
// this is only reachable with a candidate JWT, and the recipient is always the token's subject —
// the client never names whose notifications it wants, so ownership cannot be forged at the API.
[ApiController]
[Authorize(Policy = Policies.CandidateOnly)]
[Route("api/v{version:apiVersion}/candidate/notifications")]
[ApiVersion("1.0")]
public sealed class CandidateNotificationsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public CandidateNotificationsController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        // CandidateOnly guarantees the token is present and carries the sub claim.
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new ListNotificationsQuery(
            NotificationRecipientType.Candidate, candidateAccountId, page, pageSize));

        return Ok(result.Value);
    }

    // The badge number. A dedicated endpoint because the client polls it much more often than it
    // opens the feed, and it must stay a bare COUNT.
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new GetUnreadNotificationCountQuery(
            NotificationRecipientType.Candidate, candidateAccountId));

        return Ok(result.Value);
    }

    // A notification that exists but is addressed to someone else is a 404, never a 403 — the
    // handler folds ownership into the lookup, so ids cannot be probed for existence.
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new MarkNotificationReadCommand(
            NotificationRecipientType.Candidate, candidateAccountId, id));

        return result.IsSuccess
            ? NoContent()
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var candidateAccountId = _currentUser.UserId!.Value;

        await _sender.Send(new MarkAllNotificationsReadCommand(
            NotificationRecipientType.Candidate, candidateAccountId));

        return NoContent();
    }
}
