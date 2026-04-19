using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.GetAppUsageSummary;

public record GetAppUsageSummaryQuery : IRequest<AppUsageSummaryDto>;

public class GetAppUsageSummaryQueryHandler : IRequestHandler<GetAppUsageSummaryQuery, AppUsageSummaryDto>
{
    private readonly IIdentityService _identityService;

    public GetAppUsageSummaryQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<AppUsageSummaryDto> Handle(GetAppUsageSummaryQuery request, CancellationToken cancellationToken)
    {
        return _identityService.GetAppUsageSummaryAsync();
    }
}
