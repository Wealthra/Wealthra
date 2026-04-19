using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.GetSubscriptionPlans;

public record GetSubscriptionPlansQuery(bool IncludeInactive = false) : IRequest<List<SubscriptionPlanDto>>;

public class GetSubscriptionPlansQueryHandler : IRequestHandler<GetSubscriptionPlansQuery, List<SubscriptionPlanDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetSubscriptionPlansQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<SubscriptionPlanDto>> Handle(GetSubscriptionPlansQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.SubscriptionPlans.AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new SubscriptionPlanDto(x.Id, x.Name, x.Description, x.MonthlyOcrLimit, x.MonthlySttLimit, x.IsActive, x.CreatedOn, x.UpdatedOn))
            .ToListAsync(cancellationToken);
    }
}
