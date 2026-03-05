using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Incomes.Commands.CreateIncome;
using Wealthra.Application.Features.Incomes.Commands.DeleteIncome;
using Wealthra.Application.Features.Incomes.Commands.UpdateIncome;
using Wealthra.Application.Features.Incomes.Models;
using Wealthra.Application.Features.Incomes.Queries.GetIncomeById;
using Wealthra.Application.Features.Incomes.Queries.GetIncomes;
using Wealthra.Application.Features.Incomes.Queries.GetIncomeSummary;
using Wealthra.Application.Features.Incomes.Queries.GetIncomeGeneralInfo;

namespace Wealthra.Api.Controllers;

[Authorize]
public class IncomesController : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateIncomeCommand command)
    {
        var incomeId = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = incomeId }, incomeId);
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedList<IncomeDto>>> GetIncomes([FromQuery] GetIncomesQuery query)
    {
        var incomes = await Mediator.Send(query);
        return Ok(incomes);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<IncomeDto>> GetById(int id)
    {
        var income = await Mediator.Send(new GetIncomeByIdQuery(id));
        return Ok(income);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, UpdateIncomeCommand command)
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
        await Mediator.Send(new DeleteIncomeCommand(id));
        return NoContent();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<List<IncomeSummaryDto>>> GetSummary([FromQuery] string period = "Monthly")
    {
        var summary = await Mediator.Send(new GetIncomeSummaryQuery { Period = period });
        return Ok(summary);
    }

    [HttpGet("generalinfo")]
    public async Task<ActionResult<IncomeGeneralInfoDto>> GetGeneralInfo()
    {
        var result = await Mediator.Send(new GetIncomeGeneralInfoQuery());
        return Ok(result);
    }
}
