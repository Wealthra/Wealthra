using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Admin.Commands.CreateSubscriptionPlan;

public class CreateSubscriptionPlanCommand : IRequest<int>
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MonthlyOcrLimit { get; set; }
    public int MonthlySttLimit { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateSubscriptionPlanCommandValidator : AbstractValidator<CreateSubscriptionPlanCommand>
{
    public CreateSubscriptionPlanCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.MonthlyOcrLimit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MonthlySttLimit).GreaterThanOrEqualTo(0);
    }
}

public class CreateSubscriptionPlanCommandHandler : IRequestHandler<CreateSubscriptionPlanCommand, int>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAdminRealtimeService _adminRealtimeService;

    public CreateSubscriptionPlanCommandHandler(IApplicationDbContext dbContext, IAdminRealtimeService adminRealtimeService)
    {
        _dbContext = dbContext;
        _adminRealtimeService = adminRealtimeService;
    }

    public async Task<int> Handle(CreateSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var entity = new SubscriptionPlan
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            MonthlyOcrLimit = request.MonthlyOcrLimit,
            MonthlySttLimit = request.MonthlySttLimit,
            IsActive = request.IsActive,
            CreatedOn = DateTimeOffset.UtcNow
        };

        _dbContext.SubscriptionPlans.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _adminRealtimeService.PublishActivityAsync(
            "plan.created",
            $"Plan {entity.Name} created.",
            new SubscriptionPlanDto(entity.Id, entity.Name, entity.Description, entity.MonthlyOcrLimit, entity.MonthlySttLimit, entity.IsActive, entity.CreatedOn, entity.UpdatedOn),
            cancellationToken);

        return entity.Id;
    }
}
