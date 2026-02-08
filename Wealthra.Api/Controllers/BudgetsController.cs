using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Budgets.Commands.CreateBudget;
using Wealthra.Application.Features.Budgets.Commands.DeleteBudget;
using Wealthra.Application.Features.Budgets.Commands.UpdateBudget;
using Wealthra.Application.Features.Budgets.Models;
using Wealthra.Application.Features.Budgets.Queries.GetBudgetById;
using Wealthra.Application.Features.Budgets.Queries.GetBudgetOverview;
using Wealthra.Application.Features.Budgets.Queries.GetUserBudgets;

namespace Wealthra.Api.Controllers;

[Authorize]
public class BudgetsController : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateBudgetCommand command)
    {
        var budgetId = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = budgetId }, budgetId);
    }

    [HttpGet]
    public async Task<ActionResult<List<BudgetDto>>> GetBudgets()
    {
        var budgets = await Mediator.Send(new GetUserBudgetsQuery());
        return Ok(budgets);
    }

    [HttpGet("overview")]
    public async Task<ActionResult<BudgetOverviewDto>> GetOverview()
    {
        var overview = await Mediator.Send(new GetBudgetOverviewQuery());
        return Ok(overview);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BudgetDto>> GetById(int id)
    {
        var budget = await Mediator.Send(new GetBudgetByIdQuery(id));
        return Ok(budget);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, UpdateBudgetCommand command)
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
        await Mediator.Send(new DeleteBudgetCommand(id));
        return NoContent();
    }
}
