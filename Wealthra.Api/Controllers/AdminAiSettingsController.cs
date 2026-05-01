using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Security;
using Wealthra.Application.Features.Admin.Commands.SetAiRuntimeSettings;
using Wealthra.Application.Features.Admin.Models;
using Wealthra.Application.Features.Admin.Queries.GetAiRuntimeSettings;

namespace Wealthra.Api.Controllers;

[Authorize(Policy = AuthPolicies.AdminElevated)]
[Route("api/admin/settings/ai")]
public class AdminAiSettingsController : AdminApiController
{
    [HttpGet]
    public async Task<ActionResult<AiRuntimeSettingsDto>> Get()
        => Ok(await Mediator.Send(new GetAiRuntimeSettingsQuery()));

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] SetAiRuntimeSettingsCommand body)
    {
        await Mediator.Send(body);
        return NoContent();
    }
}
