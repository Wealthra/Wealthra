using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Security;
using Wealthra.Application.Features.Admin.Commands.SendTestSmtpEmail;

namespace Wealthra.Api.Controllers;

[Authorize(Policy = AuthPolicies.AdminElevated)]
[Route("api/admin/email")]
public class AdminEmailController : AdminApiController
{
    /// <summary>Sends a short HTML test message to verify SMTP settings (SuperAdmin/Admin only).</summary>
    [HttpPost("test-smtp")]
    public async Task<ActionResult<SendTestSmtpEmailResult>> SendTestSmtp([FromBody] SendTestSmtpEmailCommand command)
        => Ok(await Mediator.Send(command));
}
