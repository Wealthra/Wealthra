using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Wealthra.Api.Hubs;

[Authorize(Policy = "AdminOnly")]
public class AdminRealtimeHub : Hub
{
}
