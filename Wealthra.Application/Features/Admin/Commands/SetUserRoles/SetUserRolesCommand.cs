using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;

namespace Wealthra.Application.Features.Admin.Commands.SetUserRoles;

public record SetUserRolesCommand(string UserId, IReadOnlyList<string> Roles) : IRequest<Result>;

public class SetUserRolesCommandValidator : AbstractValidator<SetUserRolesCommand>
{
    public SetUserRolesCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Roles).NotNull();
    }
}

public class SetUserRolesCommandHandler : IRequestHandler<SetUserRolesCommand, Result>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAdminAuditService _adminAuditService;

    public SetUserRolesCommandHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        IAdminAuditService adminAuditService)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
        _adminAuditService = adminAuditService;
    }

    public async Task<Result> Handle(SetUserRolesCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.SetUserRolesAsync(
            _currentUserService.UserId ?? "unknown",
            request.UserId,
            request.Roles,
            cancellationToken);

        if (result.Succeeded)
        {
            await _adminAuditService.WriteAsync(
                _currentUserService.UserId ?? "unknown",
                "set_user_roles",
                request.UserId,
                new { request.Roles },
                _currentUserService.ClientIpAddress,
                cancellationToken);
        }

        return result;
    }
}
