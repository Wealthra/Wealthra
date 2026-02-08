using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.Notifications.Models;

public record NotificationDto(
    int Id,
    string Message,
    NotificationType Type,
    bool IsRead,
    DateTime CreatedOn,
    int? RelatedEntityId);
