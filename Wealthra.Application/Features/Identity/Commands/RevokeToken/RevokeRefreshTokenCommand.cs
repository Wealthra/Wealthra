using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;

namespace Wealthra.Application.Features.Identity.Commands.RevokeToken
{
    public record RevokeRefreshTokenCommand(string RefreshToken) : IRequest<Result>;

    public class RevokeRefreshTokenCommandHandler : IRequestHandler<RevokeRefreshTokenCommand, Result>
    {
        private readonly IIdentityService _identityService;

        public RevokeRefreshTokenCommandHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public async Task<Result> Handle(RevokeRefreshTokenCommand request, CancellationToken cancellationToken)
        {
            return await _identityService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        }
    }
}
