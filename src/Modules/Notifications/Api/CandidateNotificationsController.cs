using Asp.Versioning;
using Ats.Modules.Notifications.Application.Notifications;
using Ats.Modules.Notifications.Domain;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Notifications.Api;

// The recipient is always the token's subject; the client never says whose feed it wants.
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
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new ListNotificationsQuery(
            NotificationRecipientType.Candidate, candidateAccountId, page, pageSize));

        return Ok(result.Value);
    }

    // Separate endpoint because the client polls it often; it stays a plain COUNT.
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var candidateAccountId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new GetUnreadNotificationCountQuery(
            NotificationRecipientType.Candidate, candidateAccountId));

        return Ok(result.Value);
    }

    // Someone else's notification is a 404, so ids can't be probed.
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
