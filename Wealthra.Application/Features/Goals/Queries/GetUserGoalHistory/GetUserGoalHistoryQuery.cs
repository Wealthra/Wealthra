using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Goals.Models;

namespace Wealthra.Application.Features.Goals.Queries.GetUserGoalHistory;

public record GetUserGoalHistoryQuery : IRequest<PaginatedList<GoalHistoryDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public class GetUserGoalHistoryQueryHandler : IRequestHandler<GetUserGoalHistoryQuery, PaginatedList<GoalHistoryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUserGoalHistoryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<GoalHistoryDto>> Handle(GetUserGoalHistoryQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Goals
            .Where(g => g.CreatedBy == _currentUserService.UserId)
            .OrderByDescending(g => g.Deadline)
            .Select(g => new GoalHistoryDto(
                g.Id,
                g.Name,
                g.TargetAmount,
                g.CurrentAmount,
                g.TargetAmount > 0 ? (g.CurrentAmount / g.TargetAmount) * 100 : 0,
                g.CurrentAmount > g.TargetAmount ? g.TargetAmount : g.CurrentAmount,
                g.CurrentAmount >= g.TargetAmount ? 0 : g.TargetAmount - g.CurrentAmount,
                g.Deadline,
                g.CurrentAmount >= g.TargetAmount));

        return await PaginatedList<GoalHistoryDto>.CreateAsync(
            query,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
