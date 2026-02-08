using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Statistics.Models;
using Wealthra.Application.Features.Statistics.Queries.GetMonthlyTrends;
using Wealthra.Application.Features.Statistics.Queries.GetSpendingBreakdown;

namespace Wealthra.Api.Controllers;

[Authorize]
public class StatisticsController : ApiControllerBase
{
    [HttpGet("breakdown")]
    public async Task<ActionResult<SpendingBreakdownDto>> GetSpendingBreakdown([FromQuery] GetSpendingBreakdownQuery query)
    {
        var breakdown = await Mediator.Send(query);
        return Ok(breakdown);
    }

    [HttpGet("trends")]
    public async Task<ActionResult<MonthlyTrendsDto>> GetMonthlyTrends([FromQuery] GetMonthlyTrendsQuery query)
    {
        var trends = await Mediator.Send(query);
        return Ok(trends);
    }
}
