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
    public async Task<ActionResult<FinancialDashboardDto>> GetDashboard()
    {
        var dashboard = await Mediator.Send(new GetFinancialDashboardQuery());
        return Ok(dashboard);
    }

    [HttpGet("dashboard-web")]
    public async Task<ActionResult<DashboardWebDto>> GetDashboardWeb()
    {
        var dashboard = await Mediator.Send(new GetDashboardWebQuery());
        return Ok(dashboard);
    }
}
