using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;
using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.SupportTickets.Commands.CreateSupportTicket;

public record CreateSupportTicketCommand(string Subject, string Body) : IRequest<int>;

public class CreateSupportTicketCommandValidator : AbstractValidator<CreateSupportTicketCommand>
{
    public CreateSupportTicketCommandValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty();
    }
}

public class CreateSupportTicketCommandHandler : IRequestHandler<CreateSupportTicketCommand, int>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public CreateSupportTicketCommandHandler(IApplicationDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<int> Handle(CreateSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        var ticket = new SupportTicket
        {
            UserId = userId,
            Subject = request.Subject.Trim(),
            Body = request.Body.Trim(),
            Status = SupportTicketStatus.Open,
            CreatedBy = userId,
            CreatedOn = DateTimeOffset.UtcNow,
            LastModifiedBy = userId
        };
        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken);
        return ticket.Id;
    }
}
