using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Expenses.Commands.CreateExpense;
using Wealthra.Application.Features.Expenses.Commands.DeleteExpense;
using Wealthra.Application.Features.Expenses.Commands.UpdateExpense;
using Wealthra.Application.Features.Expenses.Models;
using Wealthra.Application.Features.Expenses.Queries.GetExpenseById;
using Wealthra.Application.Features.Expenses.Queries.GetExpenses;
using Wealthra.Application.Features.Expenses.Queries.GetUserExpenses;
using Wealthra.Application.Features.Expenses.Queries.GetExpenseSummary;
using Wealthra.Application.Features.Expenses.Queries.GetExpenseGeneralInfo;

namespace Wealthra.Api.Controllers
{
    [Authorize]
    public class ExpensesController : ApiControllerBase
    {
        private readonly IExpenseExtractionService _expenseExtractionService;

        public ExpensesController(IExpenseExtractionService expenseExtractionService)
        {
            _expenseExtractionService = expenseExtractionService;
        }

        [HttpPost]
        public async Task<ActionResult<int>> Create(CreateExpenseCommand command)
        {
            var expenseId = await Mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = expenseId }, expenseId);
        }

        [HttpPost("extract-from-image")]
        [Consumes("multipart/form-data")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = 15 * 1024 * 1024)]
        public async Task<ActionResult<IReadOnlyList<ExtractedExpenseDto>>> ExtractFromImage([FromForm] IFormFile file, CancellationToken cancellationToken)
        {
            if (file.Length == 0)
            {
                return BadRequest("Uploaded image is empty.");
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var extracted = await _expenseExtractionService.ExtractFromImageAsync(stream, file.FileName, cancellationToken);
                return Ok(extracted);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, $"OCR extraction service unavailable: {ex.Message}");
            }
        }

        [HttpPost("extract-from-audio")]
        [Consumes("multipart/form-data")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = 25 * 1024 * 1024)]
        public async Task<ActionResult<IReadOnlyList<ExtractedExpenseDto>>> ExtractFromAudio([FromForm] IFormFile file, CancellationToken cancellationToken)
        {
            if (file.Length == 0)
            {
                return BadRequest("Uploaded audio is empty.");
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var extracted = await _expenseExtractionService.ExtractFromAudioAsync(stream, file.FileName, cancellationToken);
                return Ok(extracted);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, $"STT extraction service unavailable: {ex.Message}");
            }
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

        [HttpGet("user")]
        public async Task<ActionResult<PaginatedList<ExpenseDto>>> GetUserExpenses([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var result = await Mediator.Send(new GetUserExpensesQuery { PageNumber = pageNumber, PageSize = pageSize });
            return Ok(result);
        }

        [HttpGet("summary")]
        public async Task<ActionResult<List<ExpenseSummaryDto>>> GetSummary([FromQuery] string period = "Monthly")
        {
            var summary = await Mediator.Send(new GetExpenseSummaryQuery { Period = period });
            return Ok(summary);
        }

        [HttpGet("generalinfo")]
        public async Task<ActionResult<ExpenseGeneralInfoDto>> GetGeneralInfo()
        {
            var result = await Mediator.Send(new GetExpenseGeneralInfoQuery());
            return Ok(result);
        }
    }
}
