using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Categories.Models;
using Wealthra.Application.Features.FinancialSummary.Models;
using Wealthra.Application.Features.FinancialSummary.Queries.GetDashboardWeb;
using Wealthra.Application.Features.FinancialSummary.Queries.GetFinancialDashboard;

namespace Wealthra.Api.Controllers;

[Authorize]
public class SummaryController : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<FinancialDashboardDto>> GetDashboard(
        [FromQuery] string? currency = null,
        [FromQuery] string language = "en")
    {
        if (!CategoryLanguageParser.TryParse(language, out var categoryLanguage))
        {
            return BadRequest("Invalid language. Use 'en' or 'tr'.");
        }

        var dashboard = await Mediator.Send(new GetFinancialDashboardQuery
        {
            TargetCurrency = currency,
            CategoryLanguage = categoryLanguage
        });
        return Ok(dashboard);
    }

    [HttpGet("dashboard-web")]
    public async Task<ActionResult<DashboardWebDto>> GetDashboardWeb(
        [FromQuery] string? currency = null,
        [FromQuery] string language = "en")
    {
        if (!CategoryLanguageParser.TryParse(language, out var categoryLanguage))
        {
            return BadRequest("Invalid language. Use 'en' or 'tr'.");
        }

        var dashboard = await Mediator.Send(new GetDashboardWebQuery
        {
            TargetCurrency = currency,
            CategoryLanguage = categoryLanguage
        });
        return Ok(dashboard);
    }
}
