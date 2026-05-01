using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;

namespace Wealthra.Application.Features.Admin.Commands.AdminSetPassword;

public record AdminSetPasswordCommand(string UserId, string NewPassword) : IRequest<Result>;

public class AdminSetPasswordCommandValidator : AbstractValidator<AdminSetPasswordCommand>
{
    public AdminSetPasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.NewPassword).MinimumLength(8);
    }
}

public class AdminSetPasswordCommandHandler : IRequestHandler<AdminSetPasswordCommand, Result>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAdminAuditService _adminAuditService;

    public AdminSetPasswordCommandHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        IAdminAuditService adminAuditService)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
        _adminAuditService = adminAuditService;
    }

    public async Task<Result> Handle(AdminSetPasswordCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.AdminSetPasswordAsync(request.UserId, request.NewPassword, cancellationToken);
        if (result.Succeeded)
        {
            await _adminAuditService.WriteAsync(
                _currentUserService.UserId ?? "unknown",
                "admin_set_password",
                request.UserId,
                null,
                _currentUserService.ClientIpAddress,
                cancellationToken);
        }

        return result;
    }
}
