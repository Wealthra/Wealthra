using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Analytics.Queries.GetMonthlyCategoryMetrics;

namespace Wealthra.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ApiControllerBase
    {
        [HttpGet("metrics")]
        public async Task<ActionResult<List<MonthlyCategoryMetricDto>>> GetMetrics(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] string? currency = null)
        {
            if (year == 0 || month == 0)
            {
                var now = DateTime.UtcNow;
                year = now.Year;
                month = now.Month;
            }

            var query = new GetMonthlyCategoryMetricsQuery
            {
                Year = year,
                Month = month,
                TargetCurrency = currency
            };

            var metrics = await Mediator.Send(query);
            return Ok(metrics);
        }
    }
}
