using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.GetAdminUsersPage;

public record GetAdminUsersPageQuery(int PageNumber = 1, int PageSize = 20, string? Search = null)
    : IRequest<PaginatedList<AdminUserListItemDto>>;

public class GetAdminUsersPageQueryHandler : IRequestHandler<GetAdminUsersPageQuery, PaginatedList<AdminUserListItemDto>>
{
    private readonly IIdentityService _identityService;

    public GetAdminUsersPageQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<PaginatedList<AdminUserListItemDto>> Handle(GetAdminUsersPageQuery request, CancellationToken cancellationToken)
        => _identityService.GetAdminUsersPageAsync(request.PageNumber, request.PageSize, request.Search, cancellationToken);
}
