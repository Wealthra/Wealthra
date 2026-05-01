using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.GetGrowthAnalytics;

public record GetGrowthAnalyticsQuery : IRequest<GrowthAnalyticsDto>;

public class GetGrowthAnalyticsQueryHandler : IRequestHandler<GetGrowthAnalyticsQuery, GrowthAnalyticsDto>
{
    private readonly IIdentityService _identityService;

    public GetGrowthAnalyticsQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<GrowthAnalyticsDto> Handle(GetGrowthAnalyticsQuery request, CancellationToken cancellationToken)
        => _identityService.GetGrowthAnalyticsAsync(cancellationToken);
}
