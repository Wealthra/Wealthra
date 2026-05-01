using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Security;
using Wealthra.Application.Features.Admin.Models;
using Wealthra.Application.Features.Admin.Queries.GetAiUsageSummary;
using Wealthra.Application.Features.Admin.Queries.ListAdminAuditLogs;
using Wealthra.Application.Features.Admin.Queries.ListApiErrorLogs;

namespace Wealthra.Api.Controllers;

[Authorize(Policy = AuthPolicies.AnyStaff)]
[Route("api/admin/monitoring")]
public class AdminMonitoringController : AdminApiController
{
    [HttpGet("errors")]
    public async Task<ActionResult<List<ApiErrorLogDto>>> Errors([FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] int? statusCode = null)
        => Ok(await Mediator.Send(new ListApiErrorLogsQuery(skip, take, statusCode)));

    [HttpGet("audit")]
    public async Task<ActionResult<List<AdminAuditLogDto>>> Audit([FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] string? actorUserId = null)
        => Ok(await Mediator.Send(new ListAdminAuditLogsQuery(skip, take, actorUserId)));

    [HttpGet("ai-usage")]
    public async Task<ActionResult<AiUsageSummaryDto>> AiUsage([FromQuery] int days = 7)
        => Ok(await Mediator.Send(new GetAiUsageSummaryQuery(days)));
}
