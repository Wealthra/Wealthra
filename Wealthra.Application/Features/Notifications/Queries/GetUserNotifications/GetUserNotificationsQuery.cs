using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Notifications.Models;

namespace Wealthra.Application.Features.Notifications.Queries.GetUserNotifications;

public record GetUserNotificationsQuery : IRequest<List<NotificationDto>>
{
    public bool UnreadOnly { get; init; } = true;
}

public class GetUserNotificationsQueryHandler : IRequestHandler<GetUserNotificationsQuery, List<NotificationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUserNotificationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<NotificationDto>> Handle(GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == _currentUserService.UserId);

        if (request.UnreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedOn)
            .Select(n => new NotificationDto(
                n.Id,
                n.Message,
                n.Type,
                n.IsRead,
                n.CreatedOn,
                n.RelatedEntityId))
            .ToListAsync(cancellationToken);

        return notifications;
    }
}
