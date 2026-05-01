using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.GetAdminUserDetail;

public record GetAdminUserDetailQuery(string UserId) : IRequest<AdminUserDetailDto?>;

public class GetAdminUserDetailQueryHandler : IRequestHandler<GetAdminUserDetailQuery, AdminUserDetailDto?>
{
    private readonly IIdentityService _identityService;

    public GetAdminUserDetailQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<AdminUserDetailDto?> Handle(GetAdminUserDetailQuery request, CancellationToken cancellationToken)
        => _identityService.GetAdminUserDetailAsync(request.UserId, cancellationToken);
}
