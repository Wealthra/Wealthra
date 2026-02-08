using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Expenses.Commands.CreateExpense;
using Wealthra.Application.Features.Expenses.Commands.DeleteExpense;
using Wealthra.Application.Features.Expenses.Commands.UpdateExpense;
using Wealthra.Application.Features.Expenses.Models;
using Wealthra.Application.Features.Expenses.Queries.GetExpenseById;
using Wealthra.Application.Features.Expenses.Queries.GetExpenses;
using Wealthra.Application.Features.Expenses.Queries.GetExpenseSummary;

namespace Wealthra.Api.Controllers
{
    [Authorize]
    public class ExpensesController : ApiControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<int>> Create(CreateExpenseCommand command)
        {
            var expenseId = await Mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = expenseId }, expenseId);
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedList<ExpenseDto>>> GetExpenses([FromQuery] GetExpensesQuery query)
        {
            var expenses = await Mediator.Send(query);
            return Ok(expenses);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ExpenseDto>> GetById(int id)
        {
            var expense = await Mediator.Send(new GetExpenseByIdQuery(id));
            return Ok(expense);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> Update(int id, UpdateExpenseCommand command)
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
            await Mediator.Send(new DeleteExpenseCommand(id));
            return NoContent();
        }

        [HttpGet("summary")]
        public async Task<ActionResult<List<ExpenseSummaryDto>>> GetSummary([FromQuery] string period = "Monthly")
        {
            var summary = await Mediator.Send(new GetExpenseSummaryQuery { Period = period });
            return Ok(summary);
        }
    }
}
