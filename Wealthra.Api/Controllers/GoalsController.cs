using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Goals.Commands.CreateGoal;
using Wealthra.Application.Features.Goals.Commands.DeleteGoal;
using Wealthra.Application.Features.Goals.Commands.UpdateGoal;
using Wealthra.Application.Features.Goals.Models;
using Wealthra.Application.Features.Goals.Queries.GetGoalById;
using Wealthra.Application.Features.Goals.Queries.GetUserGoals;

namespace Wealthra.Api.Controllers;

[Authorize]
public class GoalsController : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateGoalCommand command)
    {
        var goalId = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = goalId }, goalId);
    }

    [HttpGet]
    public async Task<ActionResult<List<GoalDto>>> GetGoals()
    {
        var goals = await Mediator.Send(new GetUserGoalsQuery());
        return Ok(goals);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GoalDto>> GetById(int id)
    {
        var goal = await Mediator.Send(new GetGoalByIdQuery(id));
        return Ok(goal);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, UpdateGoalCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("ID mismatch");
        }

        await Mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        await Mediator.Send(new DeleteGoalCommand(id));
        return NoContent();
    }
}
