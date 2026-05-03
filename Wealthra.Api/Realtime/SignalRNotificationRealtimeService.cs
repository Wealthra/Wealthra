using Microsoft.AspNetCore.SignalR;
using Wealthra.Api.Hubs;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Notifications.Models;

namespace Wealthra.Api.Realtime;

public class SignalRNotificationRealtimeService : INotificationRealtimeService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationRealtimeService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendNotificationAsync(string userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", notification, cancellationToken);
    }
}
