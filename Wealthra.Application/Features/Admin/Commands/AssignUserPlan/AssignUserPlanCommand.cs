using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Admin.Commands.AssignUserPlan;

public class AssignUserPlanCommand : IRequest<Unit>
{
    public string Email { get; set; } = string.Empty;
    public int PlanId { get; set; }
}

public class AssignUserPlanCommandValidator : AbstractValidator<AssignUserPlanCommand>
{
    public AssignUserPlanCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PlanId).GreaterThan(0);
    }
}

public class AssignUserPlanCommandHandler : IRequestHandler<AssignUserPlanCommand, Unit>
{
    private readonly IIdentityService _identityService;

    public AssignUserPlanCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Unit> Handle(AssignUserPlanCommand request, CancellationToken cancellationToken)
    {
        var assigned = await _identityService.AssignUserPlanAsync(request.Email, request.PlanId);
        if (!assigned)
        {
            throw new NotFoundException("UserOrPlan", $"{request.Email}:{request.PlanId}");
        }

        return Unit.Value;
    }
}
