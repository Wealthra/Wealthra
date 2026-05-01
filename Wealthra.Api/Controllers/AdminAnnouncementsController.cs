using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Security;
using Wealthra.Application.Features.Announcements.Commands.CreateSystemAnnouncement;
using Wealthra.Application.Features.Announcements.Commands.DeleteSystemAnnouncement;
using Wealthra.Application.Features.Announcements.Models;
using Wealthra.Application.Features.Announcements.Queries.ListSystemAnnouncements;

namespace Wealthra.Api.Controllers;

[Authorize(Policy = AuthPolicies.SupportTeam)]
[Route("api/admin/announcements")]
public class AdminAnnouncementsController : AdminApiController
{
    [HttpGet]
    public async Task<ActionResult<List<SystemAnnouncementDto>>> List()
        => Ok(await Mediator.Send(new ListSystemAnnouncementsQuery()));

    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] CreateSystemAnnouncementCommand cmd)
        => Ok(await Mediator.Send(cmd));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteSystemAnnouncementCommand(id));
        return NoContent();
    }
}
