using Asp.Versioning;
using Ats.Modules.Notifications.Application.Notifications;
using Ats.Modules.Notifications.Domain;
using Ats.Shared.Kernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ats.Modules.Notifications.Api;

// The recipient is always the token's subject. No tenant check needed: a user id belongs to one tenant.
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/notifications")]
[ApiVersion("1.0")]
public sealed class CompanyNotificationsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public CompanyNotificationsController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new ListNotificationsQuery(
            NotificationRecipientType.CompanyUser, userId, page, pageSize));

        return Ok(result.Value);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new GetUnreadNotificationCountQuery(
            NotificationRecipientType.CompanyUser, userId));

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = _currentUser.UserId!.Value;

        var result = await _sender.Send(new MarkNotificationReadCommand(
            NotificationRecipientType.CompanyUser, userId, id));

        return result.IsSuccess
            ? NoContent()
            : NotFound(new { result.Error.Code, result.Error.Message });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = _currentUser.UserId!.Value;

        await _sender.Send(new MarkAllNotificationsReadCommand(
            NotificationRecipientType.CompanyUser, userId));

        return NoContent();
    }
}
