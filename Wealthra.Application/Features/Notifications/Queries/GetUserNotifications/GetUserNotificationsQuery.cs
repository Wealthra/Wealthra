using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Notifications.Models;

namespace Wealthra.Application.Features.Notifications.Queries.GetUserNotifications;

public record GetUserNotificationsQuery : IRequest<List<NotificationDto>>
{
    public bool UnreadOnly { get; init; } = true;
    public string Language { get; init; } = "en";
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
        var normalizedLanguage = request.Language?.Trim().ToLowerInvariant() ?? "en";
        var isTurkish = normalizedLanguage == "tr";

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
                isTurkish ? n.MessageTr : n.MessageEn,
                n.Type,
                n.IsRead,
                n.CreatedOn,
                n.RelatedEntityId))
            .ToListAsync(cancellationToken);

        return notifications;
    }
}
