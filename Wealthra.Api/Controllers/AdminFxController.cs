using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Security;
using Wealthra.Application.Features.Admin.Commands.SetFxProviderOrder;
using Wealthra.Application.Features.Admin.Commands.UpsertManualExchangeRate;
using Wealthra.Application.Features.Admin.Models;
using Wealthra.Application.Features.Admin.Queries.GetFxProviderOrder;
using Wealthra.Application.Features.Admin.Queries.ListManualExchangeRates;

namespace Wealthra.Api.Controllers;

[Authorize(Policy = AuthPolicies.FinanceTeam)]
[Route("api/admin/fx")]
public class AdminFxController : AdminApiController
{
    [HttpGet("manual-rates")]
    public async Task<ActionResult<List<ManualExchangeRateDto>>> ListManual()
        => Ok(await Mediator.Send(new ListManualExchangeRatesQuery()));

    [HttpPost("manual-rates")]
    public async Task<ActionResult<int>> UpsertManual([FromBody] UpsertManualExchangeRateCommand cmd)
        => Ok(await Mediator.Send(cmd));

    [HttpGet("provider-order")]
    public async Task<ActionResult<string?>> GetOrder()
        => Ok(await Mediator.Send(new GetFxProviderOrderQuery()));

    [HttpPut("provider-order")]
    public async Task<IActionResult> SetOrder([FromBody] FxOrderBody body)
    {
        await Mediator.Send(new SetFxProviderOrderCommand(body.ProviderOrderJson));
        return NoContent();
    }

    public sealed class FxOrderBody
    {
        public string ProviderOrderJson { get; set; } = "[]";
    }
}
