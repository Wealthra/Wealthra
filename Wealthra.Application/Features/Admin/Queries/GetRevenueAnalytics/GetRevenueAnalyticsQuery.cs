using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.GetRevenueAnalytics;

public record GetRevenueAnalyticsQuery : IRequest<RevenueAnalyticsDto>;

public class GetRevenueAnalyticsQueryHandler : IRequestHandler<GetRevenueAnalyticsQuery, RevenueAnalyticsDto>
{
    private readonly IIdentityService _identityService;

    public GetRevenueAnalyticsQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<RevenueAnalyticsDto> Handle(GetRevenueAnalyticsQuery request, CancellationToken cancellationToken)
        => _identityService.GetRevenueAnalyticsAsync(cancellationToken);
}
