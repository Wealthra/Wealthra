using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.SupportTickets.Commands.ReplySupportTicket;

public record ReplySupportTicketCommand(int TicketId, string AdminReply, SupportTicketStatus Status) : IRequest<Unit>;

public class ReplySupportTicketCommandValidator : AbstractValidator<ReplySupportTicketCommand>
{
    public ReplySupportTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).GreaterThan(0);
        RuleFor(x => x.AdminReply).NotEmpty();
    }
}

public class ReplySupportTicketCommandHandler : IRequestHandler<ReplySupportTicketCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public ReplySupportTicketCommandHandler(IApplicationDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(ReplySupportTicketCommand request, CancellationToken cancellationToken)
    {
        var adminId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == request.TicketId, cancellationToken);
        if (ticket == null) throw new NotFoundException("SupportTicket", request.TicketId);

        ticket.AdminReply = request.AdminReply.Trim();
        ticket.Status = request.Status;
        ticket.LastRepliedByAdminUserId = adminId;
        ticket.LastModifiedBy = adminId;
        ticket.LastModifiedOn = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
