using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Wealthra.Application.Common.Security;

namespace Wealthra.Api.Hubs;

[Authorize(Policy = AuthPolicies.AdminElevated)]
public class AdminRealtimeHub : Hub
{
}
