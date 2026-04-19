using MediatR;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Wealthra.Application.Features.Admin.Commands.DeleteSubscriptionPlan;

public record DeleteSubscriptionPlanCommand(int Id) : IRequest<Unit>;

public class DeleteSubscriptionPlanCommandHandler : IRequestHandler<DeleteSubscriptionPlanCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAdminRealtimeService _adminRealtimeService;

    public DeleteSubscriptionPlanCommandHandler(IApplicationDbContext dbContext, IAdminRealtimeService adminRealtimeService)
    {
        _dbContext = dbContext;
        _adminRealtimeService = adminRealtimeService;
    }

    public async Task<Unit> Handle(DeleteSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.SubscriptionPlans.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (plan == null)
        {
            throw new NotFoundException("SubscriptionPlan", request.Id);
        }

        // Keep historical references stable by soft-deactivating.
        plan.IsActive = false;
        plan.UpdatedOn = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _adminRealtimeService.PublishActivityAsync("plan.deleted", $"Plan {plan.Name} deleted/deactivated.", new { plan.Id }, cancellationToken);

        return Unit.Value;
    }
}
