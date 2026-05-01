using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;

namespace Wealthra.Application.Features.Admin.Commands.LockUser;

public record LockUserCommand(string UserId, bool Lockout, DateTimeOffset? LockoutEnd = null) : IRequest<Result>;

public class LockUserCommandValidator : AbstractValidator<LockUserCommand>
{
    public LockUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class LockUserCommandHandler : IRequestHandler<LockUserCommand, Result>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAdminAuditService _adminAuditService;

    public LockUserCommandHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        IAdminAuditService adminAuditService)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
        _adminAuditService = adminAuditService;
    }

    public async Task<Result> Handle(LockUserCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.SetUserLockoutAsync(
            _currentUserService.UserId ?? "unknown",
            request.UserId,
            request.Lockout,
            request.LockoutEnd,
            cancellationToken);

        if (result.Succeeded)
        {
            await _adminAuditService.WriteAsync(
                _currentUserService.UserId ?? "unknown",
                request.Lockout ? "user_lockout" : "user_unlock",
                request.UserId,
                new { request.Lockout, request.LockoutEnd },
                _currentUserService.ClientIpAddress,
                cancellationToken);
        }

        return result;
    }
}
