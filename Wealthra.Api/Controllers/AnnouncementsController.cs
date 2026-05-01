using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Announcements.Models;
using Wealthra.Application.Features.Announcements.Queries.GetActiveAnnouncements;

namespace Wealthra.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
public class AnnouncementsController : ApiControllerBase
{
    [HttpGet("active")]
    public async Task<ActionResult<List<ActiveAnnouncementBannerDto>>> Active()
        => Ok(await Mediator.Send(new GetActiveAnnouncementsQuery()));
}
