using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Goals.Models;

namespace Wealthra.Application.Features.Goals.Queries.GetGoalsTotal;

public record GetGoalsTotalQuery : IRequest<GoalsTotalDto>;

public class GetGoalsTotalQueryHandler : IRequestHandler<GetGoalsTotalQuery, GoalsTotalDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetGoalsTotalQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<GoalsTotalDto> Handle(GetGoalsTotalQuery request, CancellationToken cancellationToken)
    {
        var goals = await _context.Goals
            .Where(g => g.CreatedBy == _currentUserService.UserId)
            .Select(g => new
            {
                g.TargetAmount,
                g.CurrentAmount,
                IsCompleted = g.CurrentAmount >= g.TargetAmount
            })
            .ToListAsync(cancellationToken);

        var totalTarget = goals.Sum(g => g.TargetAmount);
        var totalCurrent = goals.Sum(g => g.CurrentAmount);
        var overallPercentage = totalTarget > 0 ? (totalCurrent / totalTarget) * 100 : 0;
        var achievedCount = goals.Count(g => g.IsCompleted);

        return new GoalsTotalDto(
            TotalTargetAmount: totalTarget,
            TotalCurrentAmount: totalCurrent,
            OverallProgressPercentage: overallPercentage,
            TotalGoals: goals.Count,
            AchievedGoals: achievedCount,
            NotAchievedGoals: goals.Count - achievedCount);
    }
}
