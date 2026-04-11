using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.FinancialSummary.Models;
using Wealthra.Application.Features.FinancialSummary.Queries.GetDashboardWeb;
using Wealthra.Application.Features.FinancialSummary.Queries.GetFinancialDashboard;

namespace Wealthra.Api.Controllers;

[Authorize]
public class SummaryController : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<FinancialDashboardDto>> GetDashboard([FromQuery] string? currency = null)
    {
        var dashboard = await Mediator.Send(new GetFinancialDashboardQuery { TargetCurrency = currency });
        return Ok(dashboard);
    }

    [HttpGet("dashboard-web")]
    public async Task<ActionResult<DashboardWebDto>> GetDashboardWeb([FromQuery] string? currency = null)
    {
        var dashboard = await Mediator.Send(new GetDashboardWebQuery { TargetCurrency = currency });
        return Ok(dashboard);
    }
}
