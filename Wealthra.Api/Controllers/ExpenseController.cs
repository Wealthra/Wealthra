using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Wealthra.Application.Features.Expenses.Commands.CreateExpense;

namespace Wealthra.Api.Controllers
{
    [Authorize] // Requires JWT
    public class ExpensesController : ApiControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<int>> Create(CreateExpenseCommand command)
        {
            var id = await Mediator.Send(command);
            return Ok(id);
        }
    }
}