using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Admin.Commands.UpdateSubscriptionPlan;

public class UpdateSubscriptionPlanCommand : IRequest<Unit>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MonthlyOcrLimit { get; set; }
    public int MonthlySttLimit { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateSubscriptionPlanCommandValidator : AbstractValidator<UpdateSubscriptionPlanCommand>
{
    public UpdateSubscriptionPlanCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.MonthlyOcrLimit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MonthlySttLimit).GreaterThanOrEqualTo(0);
    }
}

public class UpdateSubscriptionPlanCommandHandler : IRequestHandler<UpdateSubscriptionPlanCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAdminRealtimeService _adminRealtimeService;

    public UpdateSubscriptionPlanCommandHandler(IApplicationDbContext dbContext, IAdminRealtimeService adminRealtimeService)
    {
        _dbContext = dbContext;
        _adminRealtimeService = adminRealtimeService;
    }

    public async Task<Unit> Handle(UpdateSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.SubscriptionPlans.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (plan == null)
        {
            throw new NotFoundException("SubscriptionPlan", request.Id);
        }

        plan.Name = request.Name.Trim();
        plan.Description = request.Description.Trim();
        plan.MonthlyOcrLimit = request.MonthlyOcrLimit;
        plan.MonthlySttLimit = request.MonthlySttLimit;
        plan.IsActive = request.IsActive;
        plan.UpdatedOn = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _adminRealtimeService.PublishActivityAsync("plan.updated", $"Plan {plan.Name} updated.", new { plan.Id }, cancellationToken);

        return Unit.Value;
    }
}
