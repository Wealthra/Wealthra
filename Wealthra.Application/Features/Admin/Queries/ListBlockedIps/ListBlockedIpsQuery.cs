using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.ListBlockedIps;

public record ListBlockedIpsQuery : IRequest<List<BlockedIpDto>>;

public class ListBlockedIpsQueryHandler : IRequestHandler<ListBlockedIpsQuery, List<BlockedIpDto>>
{
    private readonly IApplicationDbContext _db;

    public ListBlockedIpsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<BlockedIpDto>> Handle(ListBlockedIpsQuery request, CancellationToken cancellationToken)
    {
        return await _db.BlockedIpAddresses.AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => new BlockedIpDto(x.Id, x.IpAddress, x.Reason, x.CreatedUtc, x.ExpiresUtc))
            .ToListAsync(cancellationToken);
    }
}
