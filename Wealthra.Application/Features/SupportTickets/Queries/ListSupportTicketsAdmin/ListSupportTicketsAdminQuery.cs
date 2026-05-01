using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.SupportTickets.Models;
using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.SupportTickets.Queries.ListSupportTicketsAdmin;

public record ListSupportTicketsAdminQuery(SupportTicketStatus? Status = null, int Take = 100) : IRequest<List<SupportTicketDto>>;

public class ListSupportTicketsAdminQueryHandler : IRequestHandler<ListSupportTicketsAdminQuery, List<SupportTicketDto>>
{
    private readonly IApplicationDbContext _db;

    public ListSupportTicketsAdminQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<SupportTicketDto>> Handle(ListSupportTicketsAdminQuery request, CancellationToken cancellationToken)
    {
        var q = _db.SupportTickets.AsNoTracking().AsQueryable();
        if (request.Status.HasValue)
        {
            q = q.Where(x => x.Status == request.Status.Value);
        }

        return await q
            .OrderByDescending(x => x.CreatedOn)
            .Take(Math.Clamp(request.Take, 1, 500))
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
