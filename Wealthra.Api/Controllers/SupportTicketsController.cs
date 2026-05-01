using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Security;
using Wealthra.Application.Features.SupportTickets.Commands.CreateSupportTicket;
using Wealthra.Application.Features.SupportTickets.Commands.ReplySupportTicket;
using Wealthra.Application.Features.SupportTickets.Models;
using Wealthra.Application.Features.SupportTickets.Queries.ListMySupportTickets;
using Wealthra.Application.Features.SupportTickets.Queries.ListSupportTicketsAdmin;
using Wealthra.Domain.Enums;

namespace Wealthra.Api.Controllers;

[Authorize]
[Route("api/support/tickets")]
public class SupportTicketsController : ApiControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<List<SupportTicketDto>>> Mine()
        => Ok(await Mediator.Send(new ListMySupportTicketsQuery()));

    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] CreateSupportTicketCommand cmd)
        => Ok(await Mediator.Send(cmd));

    [Authorize(Policy = AuthPolicies.SupportTeam)]
    [HttpGet("admin")]
    public async Task<ActionResult<List<SupportTicketDto>>> AdminList([FromQuery] SupportTicketStatus? status = null, [FromQuery] int take = 100)
        => Ok(await Mediator.Send(new ListSupportTicketsAdminQuery(status, take)));

    [Authorize(Policy = AuthPolicies.SupportTeam)]
    [HttpPost("{id:int}/reply")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyBody body)
    {
        await Mediator.Send(new ReplySupportTicketCommand(id, body.AdminReply, body.Status));
        return NoContent();
    }

    public sealed class ReplyBody
    {
        public string AdminReply { get; set; } = string.Empty;
        public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Resolved;
    }
}
