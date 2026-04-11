using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Notifications.Commands.ClearNotifications;
using Wealthra.Application.Features.Notifications.Commands.MarkNotificationsRead;
using Wealthra.Application.Features.Notifications.Models;
using Wealthra.Application.Features.Notifications.Queries.GetUserNotifications;

namespace Wealthra.Api.Controllers;

[Authorize]
public class NotificationsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetNotifications([FromQuery] bool unreadOnly = true, [FromQuery] string language = "en")
    {
        var notifications = await Mediator.Send(new GetUserNotificationsQuery
        {
            UnreadOnly = unreadOnly,
            Language = language
        });
        return Ok(notifications);
    }

    [HttpPost("mark-read")]
    public async Task<ActionResult> MarkAsRead(MarkNotificationsReadCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }

    [HttpDelete]
    public async Task<ActionResult> ClearNotifications(ClearAllNotificationsCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }
}
