using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Security;
using Wealthra.Application.Features.Admin.Commands.DeleteManualExchangeRate;
using Wealthra.Application.Features.Admin.Commands.SetFxProviderOrder;
using Wealthra.Application.Features.Admin.Commands.UpdateManualExchangeRate;
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

    [HttpPut("manual-rates/{id:int}")]
    public async Task<IActionResult> UpdateManual(int id, [FromBody] ManualRateUpdateBody body)
    {
        await Mediator.Send(new UpdateManualExchangeRateCommand(id, body.Rate));
        return NoContent();
    }

    [HttpDelete("manual-rates/{id:int}")]
    public async Task<IActionResult> DeleteManual(int id)
    {
        await Mediator.Send(new DeleteManualExchangeRateCommand(id));
        return NoContent();
    }

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

    public sealed class ManualRateUpdateBody
    {
        public decimal Rate { get; set; }
    }
}
