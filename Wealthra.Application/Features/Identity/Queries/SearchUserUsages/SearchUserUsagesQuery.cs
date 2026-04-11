using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Models;

namespace Wealthra.Application.Features.Identity.Queries.SearchUserUsages
{
    public class SearchUserUsagesQuery : IRequest<List<UserUsageDto>>
    {
        public string? Email { get; set; }
        public string? Name { get; set; }
    }

    public class SearchUserUsagesQueryHandler : IRequestHandler<SearchUserUsagesQuery, List<UserUsageDto>>
    {
        private readonly IIdentityService _identityService;

        public SearchUserUsagesQueryHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public async Task<List<UserUsageDto>> Handle(SearchUserUsagesQuery request, CancellationToken cancellationToken)
        {
            return await _identityService.SearchUserUsagesAsync(request.Email, request.Name);
        }
    }
}
