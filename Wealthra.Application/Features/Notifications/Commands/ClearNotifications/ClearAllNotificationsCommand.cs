using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Notifications.Commands.ClearNotifications;

public record ClearAllNotificationsCommand : IRequest<Unit>
{
    public List<int>? NotificationIds { get; init; }
    public bool ClearAll { get; init; } = false;
}

public class ClearAllNotificationsCommandValidator : AbstractValidator<ClearAllNotificationsCommand>
{
    public ClearAllNotificationsCommandValidator()
    {
        RuleFor(v => v)
            .Must(v => v.ClearAll || (v.NotificationIds != null && v.NotificationIds.Any()))
            .WithMessage("Either ClearAll must be true or NotificationIds must be provided.");
    }
}

public class ClearAllNotificationsCommandHandler : IRequestHandler<ClearAllNotificationsCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ClearAllNotificationsCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(ClearAllNotificationsCommand request, CancellationToken cancellationToken)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == _currentUserService.UserId);

        if (!request.ClearAll && request.NotificationIds != null)
        {
            query = query.Where(n => request.NotificationIds.Contains(n.Id));
        }

        var notifications = await query.ToListAsync(cancellationToken);

        _context.Notifications.RemoveRange(notifications);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
