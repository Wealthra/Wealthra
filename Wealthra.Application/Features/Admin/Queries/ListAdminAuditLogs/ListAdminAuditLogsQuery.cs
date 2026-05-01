using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.ListAdminAuditLogs;

public record ListAdminAuditLogsQuery(int Skip = 0, int Take = 50, string? ActorUserId = null)
    : IRequest<List<AdminAuditLogDto>>;

public class ListAdminAuditLogsQueryHandler : IRequestHandler<ListAdminAuditLogsQuery, List<AdminAuditLogDto>>
{
    private readonly IApplicationDbContext _db;

    public ListAdminAuditLogsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<AdminAuditLogDto>> Handle(ListAdminAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var q = _db.AdminAuditLogs.AsNoTracking().OrderByDescending(x => x.CreatedUtc);
        if (!string.IsNullOrWhiteSpace(request.ActorUserId))
        {
            q = q.Where(x => x.ActorUserId == request.ActorUserId);
        }

        return await q
            .Skip(request.Skip)
            .Take(Math.Clamp(request.Take, 1, 200))
            .Select(x => new AdminAuditLogDto(
                x.Id,
                x.ActorUserId,
                x.Action,
                x.TargetUserId,
                x.DetailsJson,
                x.IpAddress,
                x.CreatedUtc))
            .ToListAsync(cancellationToken);
    }
}
