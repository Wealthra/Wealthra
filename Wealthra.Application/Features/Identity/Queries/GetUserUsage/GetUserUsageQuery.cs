using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Models;

namespace Wealthra.Application.Features.Identity.Queries.GetUserUsage
{
    public class GetUserUsageQuery : IRequest<UserUsageDto>
    {
    }

    public class GetUserUsageQueryHandler : IRequestHandler<GetUserUsageQuery, UserUsageDto>
    {
        private readonly IIdentityService _identityService;
        private readonly ICurrentUserService _currentUserService;

        public GetUserUsageQueryHandler(IIdentityService identityService, ICurrentUserService currentUserService)
        {
            _identityService = identityService;
            _currentUserService = currentUserService;
        }

        public async Task<UserUsageDto> Handle(GetUserUsageQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException();
            }

            var usage = await _identityService.GetUserUsageAsync(userId);
            if (usage == null)
            {
                throw new NotFoundException("User", userId);
            }

            return usage;
        }
    }
}
