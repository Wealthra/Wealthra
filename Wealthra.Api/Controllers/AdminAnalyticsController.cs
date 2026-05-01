using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Security;
using Wealthra.Application.Features.Admin.Models;
using Wealthra.Application.Features.Admin.Queries.GetGrowthAnalytics;
using Wealthra.Application.Features.Admin.Queries.GetRevenueAnalytics;

namespace Wealthra.Api.Controllers;

[Authorize(Policy = AuthPolicies.FinanceTeam)]
[Route("api/admin/analytics")]
public class AdminAnalyticsController : AdminApiController
{
    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueAnalyticsDto>> Revenue()
        => Ok(await Mediator.Send(new GetRevenueAnalyticsQuery()));

    [HttpGet("growth")]
    public async Task<ActionResult<GrowthAnalyticsDto>> Growth()
        => Ok(await Mediator.Send(new GetGrowthAnalyticsQuery()));
}
