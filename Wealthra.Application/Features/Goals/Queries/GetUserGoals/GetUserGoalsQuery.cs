using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Goals.Models;

namespace Wealthra.Application.Features.Goals.Queries.GetUserGoals;

public record GetUserGoalsQuery : IRequest<List<GoalDto>>;

public class GetUserGoalsQueryHandler : IRequestHandler<GetUserGoalsQuery, List<GoalDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUserGoalsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<GoalDto>> Handle(GetUserGoalsQuery request, CancellationToken cancellationToken)
    {
        var goals = await _context.Goals
            .Where(g => g.CreatedBy == _currentUserService.UserId)
            .OrderBy(g => g.Deadline)
            .Select(g => new GoalDto(
                g.Id,
                g.Name,
                g.TargetAmount,
                g.CurrentAmount,
                g.TargetAmount > 0 ? (g.CurrentAmount / g.TargetAmount) * 100 : 0,
                g.Deadline,
                g.CurrentAmount >= g.TargetAmount))
            .ToListAsync(cancellationToken);

        return goals;
    }
}
