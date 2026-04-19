using Microsoft.AspNetCore.SignalR;
using Wealthra.Api.Hubs;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Api.Realtime;

public class SignalRAdminRealtimeService : IAdminRealtimeService
{
    private readonly IHubContext<AdminRealtimeHub> _hubContext;

    public SignalRAdminRealtimeService(IHubContext<AdminRealtimeHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishActivityAsync(string activityType, string message, object? payload = null, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "admin.activity",
            new AdminActivityEvent(activityType, message, DateTimeOffset.UtcNow, payload),
            cancellationToken);
    }

    public Task PublishSnapshotAsync(object snapshot, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.SendAsync(
            "admin.snapshot",
            new AdminSnapshotEvent(DateTimeOffset.UtcNow, snapshot),
            cancellationToken);
    }
}
