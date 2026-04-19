using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Models;

namespace Wealthra.Application.Features.Admin.Queries.GetUsersByPlan;

public record GetUsersByPlanQuery(int PlanId) : IRequest<List<UserUsageDto>>;

public class GetUsersByPlanQueryHandler : IRequestHandler<GetUsersByPlanQuery, List<UserUsageDto>>
{
    private readonly IIdentityService _identityService;

    public GetUsersByPlanQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<List<UserUsageDto>> Handle(GetUsersByPlanQuery request, CancellationToken cancellationToken)
    {
        return _identityService.GetUsersByPlanAsync(request.PlanId);
    }
}
