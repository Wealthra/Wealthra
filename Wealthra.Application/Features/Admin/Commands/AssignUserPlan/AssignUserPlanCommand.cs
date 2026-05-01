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
    private readonly IAdminAuditService _adminAuditService;
    private readonly ICurrentUserService _currentUserService;

    public AssignUserPlanCommandHandler(
        IIdentityService identityService,
        IAdminAuditService adminAuditService,
        ICurrentUserService currentUserService)
    {
        _identityService = identityService;
        _adminAuditService = adminAuditService;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(AssignUserPlanCommand request, CancellationToken cancellationToken)
    {
        var assigned = await _identityService.AssignUserPlanAsync(request.Email, request.PlanId);
        if (!assigned)
        {
            throw new NotFoundException("UserOrPlan", $"{request.Email}:{request.PlanId}");
        }

        await _adminAuditService.WriteAsync(
            _currentUserService.UserId ?? "unknown",
            "assign_user_plan",
            null,
            new { request.Email, request.PlanId },
            _currentUserService.ClientIpAddress,
            cancellationToken);

        return Unit.Value;
    }
}
