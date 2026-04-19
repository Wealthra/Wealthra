using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Admin.Commands.AssignUserPlan;
using Wealthra.Application.Features.Admin.Commands.CreateSubscriptionPlan;
using Wealthra.Application.Features.Admin.Commands.DeleteSubscriptionPlan;
using Wealthra.Application.Features.Admin.Commands.UpdateSubscriptionPlan;
using Wealthra.Application.Features.Admin.Models;
using Wealthra.Application.Features.Admin.Queries.GetAppUsageSummary;
using Wealthra.Application.Features.Admin.Queries.GetSubscriptionPlans;
using Wealthra.Application.Features.Admin.Queries.GetUsersByPlan;

namespace Wealthra.Api.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminPlansController : ApiControllerBase
{
    [HttpGet("plans")]
    public async Task<ActionResult<List<SubscriptionPlanDto>>> GetPlans([FromQuery] bool includeInactive = false)
    {
        var plans = await Mediator.Send(new GetSubscriptionPlansQuery(includeInactive));
        return Ok(plans);
    }

    [HttpPost("plans")]
    public async Task<ActionResult<int>> CreatePlan(CreateSubscriptionPlanCommand command)
    {
        var id = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetPlans), new { id }, id);
    }

    [HttpPut("plans/{id:int}")]
    public async Task<ActionResult> UpdatePlan(int id, UpdateSubscriptionPlanCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("ID mismatch");
        }

        await Mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("plans/{id:int}")]
    public async Task<ActionResult> DeletePlan(int id)
    {
        await Mediator.Send(new DeleteSubscriptionPlanCommand(id));
        return NoContent();
    }

    [HttpPut("plans/assign")]
    public async Task<ActionResult> AssignPlan(AssignUserPlanCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }

    [HttpGet("plans/{id:int}/users")]
    public async Task<ActionResult<List<Wealthra.Application.Features.Identity.Models.UserUsageDto>>> GetUsersByPlan(int id)
    {
        var users = await Mediator.Send(new GetUsersByPlanQuery(id));
        return Ok(users);
    }

    [HttpGet("usage/summary")]
    public async Task<ActionResult<AppUsageSummaryDto>> GetUsageSummary()
    {
        var summary = await Mediator.Send(new GetAppUsageSummaryQuery());
        return Ok(summary);
    }
}
