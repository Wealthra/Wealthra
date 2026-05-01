using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;

namespace Wealthra.Application.Features.Admin.Commands.RevokeUserSessions;

public record RevokeUserSessionsCommand(string UserId) : IRequest<Result>;

public class RevokeUserSessionsCommandValidator : AbstractValidator<RevokeUserSessionsCommand>
{
    public RevokeUserSessionsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class RevokeUserSessionsCommandHandler : IRequestHandler<RevokeUserSessionsCommand, Result>
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAdminAuditService _adminAuditService;

    public RevokeUserSessionsCommandHandler(
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        IAdminAuditService adminAuditService)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
        _adminAuditService = adminAuditService;
    }

    public async Task<Result> Handle(RevokeUserSessionsCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.RevokeAllRefreshTokensAsync(request.UserId, cancellationToken);
        if (result.Succeeded)
        {
            await _adminAuditService.WriteAsync(
                _currentUserService.UserId ?? "unknown",
                "revoke_user_sessions",
                request.UserId,
                null,
                _currentUserService.ClientIpAddress,
                cancellationToken);
        }

        return result;
    }
}
