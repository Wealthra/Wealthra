using Wealthra.Application.Features.Notifications.Models;

namespace Wealthra.Application.Common.Interfaces;

public interface INotificationRealtimeService
{
    Task SendNotificationAsync(string userId, NotificationDto notification, CancellationToken cancellationToken = default);
}
