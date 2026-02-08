using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Models;

namespace Wealthra.Application.Features.Identity.Queries.GetMyProfile
{
    public record GetMyProfileQuery : IRequest<UserDto>;

    public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, UserDto>
    {
        private readonly IIdentityService _identityService;
        private readonly ICurrentUserService _currentUserService;

        public GetMyProfileQueryHandler(IIdentityService identityService, ICurrentUserService currentUserService)
        {
            _identityService = identityService;
            _currentUserService = currentUserService;
        }

        public async Task<UserDto> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentUserService.UserId))
            {
                throw new UnauthorizedAccessException();
            }

            var user = await _identityService.GetUserDetailsAsync(_currentUserService.UserId);

            if (user == null)
            {
                throw new NotFoundException("User", _currentUserService.UserId);
            }

            return user;
        }
    }
}