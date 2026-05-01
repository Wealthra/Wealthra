using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Announcements.Models;

namespace Wealthra.Application.Features.Announcements.Queries.ListSystemAnnouncements;

public record ListSystemAnnouncementsQuery : IRequest<List<SystemAnnouncementDto>>;

public class ListSystemAnnouncementsQueryHandler : IRequestHandler<ListSystemAnnouncementsQuery, List<SystemAnnouncementDto>>
{
    private readonly IApplicationDbContext _db;

    public ListSystemAnnouncementsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<SystemAnnouncementDto>> Handle(ListSystemAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        return await _db.SystemAnnouncements.AsNoTracking()
            .OrderByDescending(x => x.StartsAt)
            .Select(x => new SystemAnnouncementDto(
                x.Id,
                x.TitleEn,
                x.TitleTr,
                x.BodyEn,
                x.BodyTr,
                x.Severity,
                x.StartsAt,
                x.EndsAt,
                x.TargetAllSubscribers,
                x.TargetPlanIdsJson,
                x.TargetTiersJson,
                x.IsPublished))
            .ToListAsync(cancellationToken);
    }
}
