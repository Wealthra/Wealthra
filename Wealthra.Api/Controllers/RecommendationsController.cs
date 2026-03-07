using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Recommendations.Commands.AnalyzeSpendingAnomalies;

namespace Wealthra.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RecommendationsController : ApiControllerBase
    {
        [HttpPost("analyze")]
        public async Task<ActionResult<List<string>>> AnalyzeAnomalies([FromQuery] int year, [FromQuery] int month)
        {
            if (year == 0 || month == 0)
            {
                var now = DateTime.UtcNow;
                year = now.Year;
                month = now.Month;
            }

            var command = new AnalyzeSpendingAnomaliesCommand
            {
                Year = year,
                Month = month
            };

            var alerts = await Mediator.Send(command);
            return Ok(alerts);
        }
    }
}
