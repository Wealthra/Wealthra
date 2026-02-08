using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.FinancialSummary.Models;
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
}
