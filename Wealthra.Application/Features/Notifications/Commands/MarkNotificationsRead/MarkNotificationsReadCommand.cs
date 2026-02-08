using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Notifications.Commands.MarkNotificationsRead;

public record MarkNotificationsReadCommand : IRequest<Unit>
{
    public List<int>? NotificationIds { get; init; }
    public bool MarkAll { get; init; } = false;
}

public class MarkNotificationsReadCommandValidator : AbstractValidator<MarkNotificationsReadCommand>
{
    public MarkNotificationsReadCommandValidator()
    {
        RuleFor(v => v)
            .Must(v => v.MarkAll || (v.NotificationIds != null && v.NotificationIds.Any()))
            .WithMessage("Either MarkAll must be true or NotificationIds must be provided.");
    }
}

public class MarkNotificationsReadCommandHandler : IRequestHandler<MarkNotificationsReadCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkNotificationsReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == _currentUserService.UserId && !n.IsRead);

        if (!request.MarkAll && request.NotificationIds != null)
        {
            query = query.Where(n => request.NotificationIds.Contains(n.Id));
        }

        var notifications = await query.ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
