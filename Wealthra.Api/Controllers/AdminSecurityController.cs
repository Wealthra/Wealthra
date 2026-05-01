using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Security;
using Wealthra.Application.Features.Admin.Commands.BlockIp;
using Wealthra.Application.Features.Admin.Commands.UnblockIp;
using Wealthra.Application.Features.Admin.Models;
using Wealthra.Application.Features.Admin.Queries.ListBlockedIps;

namespace Wealthra.Api.Controllers;

[Authorize(Policy = AuthPolicies.AdminElevated)]
[Route("api/admin/security")]
public class AdminSecurityController : AdminApiController
{
    [HttpGet("blocked-ips")]
    public async Task<ActionResult<List<BlockedIpDto>>> List()
        => Ok(await Mediator.Send(new ListBlockedIpsQuery()));

    [HttpPost("blocked-ips")]
    public async Task<ActionResult<int>> Block([FromBody] BlockIpCommand cmd)
        => Ok(await Mediator.Send(cmd));

    [HttpDelete("blocked-ips/{ip}")]
    public async Task<IActionResult> Unblock(string ip)
    {
        await Mediator.Send(new UnblockIpCommand(ip));
        return NoContent();
    }
}
