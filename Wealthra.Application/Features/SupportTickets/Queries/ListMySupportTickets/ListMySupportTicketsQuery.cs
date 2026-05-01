using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.SupportTickets.Models;

namespace Wealthra.Application.Features.SupportTickets.Queries.ListMySupportTickets;

public record ListMySupportTicketsQuery : IRequest<List<SupportTicketDto>>;

public class ListMySupportTicketsQueryHandler : IRequestHandler<ListMySupportTicketsQuery, List<SupportTicketDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public ListMySupportTicketsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<List<SupportTicketDto>> Handle(ListMySupportTicketsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        return await _db.SupportTickets.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedOn)
            .Select(x => new SupportTicketDto(
                x.Id,
                x.UserId,
                x.Subject,
                x.Body,
                x.Status,
                x.AdminReply,
                x.CreatedOn,
                x.LastModifiedOn))
            .ToListAsync(cancellationToken);
    }
}
