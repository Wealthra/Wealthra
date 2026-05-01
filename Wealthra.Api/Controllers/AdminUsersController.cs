using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Common.Security;
using Wealthra.Application.Features.Admin.Commands.AdminSetPassword;
using Wealthra.Application.Features.Admin.Commands.LockUser;
using Wealthra.Application.Features.Admin.Commands.RevokeUserSessions;
using Wealthra.Application.Features.Admin.Commands.SetUserRoles;
using Wealthra.Application.Features.Admin.Models;
using Wealthra.Application.Features.Admin.Queries.GetAdminUserDetail;
using Wealthra.Application.Features.Admin.Queries.GetAdminUsersPage;

namespace Wealthra.Api.Controllers;

[Authorize(Policy = AuthPolicies.AnyStaff)]
[Route("api/admin/users")]
public class AdminUsersController : AdminApiController
{
    [HttpGet]
    public async Task<ActionResult<PaginatedList<AdminUserListItemDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        var result = await Mediator.Send(new GetAdminUsersPageQuery(page, pageSize, search));
        return Ok(result);
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<AdminUserDetailDto>> Get(string userId)
    {
        var u = await Mediator.Send(new GetAdminUserDetailQuery(userId));
        return u == null ? NotFound() : Ok(u);
    }

    [Authorize(Policy = AuthPolicies.AdminElevated)]
    [HttpPost("{userId}/lock")]
    public async Task<IActionResult> Lock(string userId, [FromBody] LockUserBody body)
    {
        var result = await Mediator.Send(new LockUserCommand(userId, body.Lockout, body.LockoutEnd));
        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    [Authorize(Policy = AuthPolicies.AdminElevated)]
    [HttpPut("{userId}/roles")]
    public async Task<IActionResult> Roles(string userId, [FromBody] IReadOnlyList<string> roles)
    {
        var result = await Mediator.Send(new SetUserRolesCommand(userId, roles));
        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    [Authorize(Policy = AuthPolicies.AdminElevated)]
    [HttpPost("{userId}/revoke-sessions")]
    public async Task<IActionResult> RevokeSessions(string userId)
    {
        var result = await Mediator.Send(new RevokeUserSessionsCommand(userId));
        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    [Authorize(Policy = AuthPolicies.AdminElevated)]
    [HttpPost("{userId}/password")]
    public async Task<IActionResult> SetPassword(string userId, [FromBody] AdminPasswordBody body)
    {
        var result = await Mediator.Send(new AdminSetPasswordCommand(userId, body.NewPassword));
        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    public sealed class LockUserBody
    {
        public bool Lockout { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
    }

    public sealed class AdminPasswordBody
    {
        public string NewPassword { get; set; } = string.Empty;
    }
}
