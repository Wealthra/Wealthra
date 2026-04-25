using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Features.Export.Queries.ExportFinancialData;

namespace Wealthra.Api.Controllers;

[Authorize]
public class ExportController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ExportData(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string format = "pdf",
        [FromQuery] string? currency = null,
        [FromQuery] string lang = "en")
    {
        var query = new ExportFinancialDataQuery
        {
            StartDate = startDate,
            EndDate = endDate,
            Format = format,
            TargetCurrency = currency,
            Language = lang
        };

        var fileDto = await Mediator.Send(query);

        return File(fileDto.Content, fileDto.ContentType, fileDto.FileName);
    }
}