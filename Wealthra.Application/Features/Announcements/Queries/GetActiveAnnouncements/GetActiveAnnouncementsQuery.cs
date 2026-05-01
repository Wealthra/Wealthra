using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Announcements.Models;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Announcements.Queries.GetActiveAnnouncements;

public record GetActiveAnnouncementsQuery : IRequest<List<ActiveAnnouncementBannerDto>>;

public class GetActiveAnnouncementsQueryHandler : IRequestHandler<GetActiveAnnouncementsQuery, List<ActiveAnnouncementBannerDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;

    public GetActiveAnnouncementsQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUserService,
        IIdentityService identityService)
    {
        _db = db;
        _currentUserService = currentUserService;
        _identityService = identityService;
    }

    public async Task<List<ActiveAnnouncementBannerDto>> Handle(GetActiveAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var usage = await _identityService.GetUserUsageAsync(userId);
        var now = DateTimeOffset.UtcNow;

        var list = await _db.SystemAnnouncements.AsNoTracking()
            .Where(a => a.IsPublished && a.StartsAt <= now && a.EndsAt >= now)
            .OrderByDescending(a => a.Severity)
            .ToListAsync(cancellationToken);

        return list
            .Where(a => MatchesUser(a, usage))
            .Select(a => new ActiveAnnouncementBannerDto(
                a.Id,
                a.TitleEn,
                a.TitleTr,
                a.BodyEn,
                a.BodyTr,
                a.Severity))
            .ToList();
    }

    private static bool MatchesUser(SystemAnnouncement a, Wealthra.Application.Features.Identity.Models.UserUsageDto? usage)
    {
        if (a.TargetAllSubscribers) return true;

        var planId = usage?.SubscriptionPlanId;
        if (!string.IsNullOrEmpty(a.TargetPlanIdsJson) && planId.HasValue)
        {
            try
            {
                var ids = JsonSerializer.Deserialize<int[]>(a.TargetPlanIdsJson);
                if (ids != null && ids.Contains(planId.Value)) return true;
            }
            catch (JsonException)
            {
                /* ignore */
            }
        }

        if (!string.IsNullOrEmpty(a.TargetTiersJson) && usage != null)
        {
            try
            {
                var tiers = JsonSerializer.Deserialize<int[]>(a.TargetTiersJson);
                if (tiers != null && tiers.Contains((int)usage.SubscriptionTier)) return true;
            }
            catch (JsonException)
            {
                /* ignore */
            }
        }

        return false;
    }
}
