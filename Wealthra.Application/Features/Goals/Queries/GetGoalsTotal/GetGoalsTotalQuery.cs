using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Goals.Models;

namespace Wealthra.Application.Features.Goals.Queries.GetGoalsTotal;

public record GetGoalsTotalQuery : IRequest<GoalsTotalDto>
{
    public string? TargetCurrency { get; init; }
}

public class GetGoalsTotalQueryHandler : IRequestHandler<GetGoalsTotalQuery, GoalsTotalDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly ICurrencyExchangeService _currencyService;

    public GetGoalsTotalQueryHandler(
        IApplicationDbContext context, 
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        ICurrencyExchangeService currencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _currencyService = currencyService;
    }

    public async Task<GoalsTotalDto> Handle(GetGoalsTotalQuery request, CancellationToken cancellationToken)
    {
        var userDetails = await _identityService.GetUserDetailsAsync(_currentUserService.UserId);
        var prefCurrency = request.TargetCurrency ?? userDetails?.PreferredCurrency ?? "TRY";

        var goals = await _context.Goals
            .Where(g => g.CreatedBy == _currentUserService.UserId)
            .Select(g => new
            {
                g.TargetAmount,
                g.CurrentAmount,
                Currency = g.Currency ?? "TRY",
                IsCompleted = g.CurrentAmount >= g.TargetAmount
            })
            .ToListAsync(cancellationToken);

        decimal totalTarget = 0;
        decimal totalCurrent = 0;
        int achievedCount = 0;

        foreach (var goal in goals)
        {
            var targetInPref = await _currencyService.ConvertAsync(goal.TargetAmount, goal.Currency, prefCurrency, cancellationToken);
            var currentInPref = await _currencyService.ConvertAsync(goal.CurrentAmount, goal.Currency, prefCurrency, cancellationToken);

            totalTarget += targetInPref;
            totalCurrent += currentInPref;
            if (goal.IsCompleted) achievedCount++;
        }

        var overallPercentage = totalTarget > 0 ? (totalCurrent / totalTarget) * 100 : 0;

        return new GoalsTotalDto(
            TotalTargetAmount: totalTarget,
            TotalCurrentAmount: totalCurrent,
            OverallProgressPercentage: overallPercentage,
            TotalGoals: goals.Count,
            AchievedGoals: achievedCount,
            NotAchievedGoals: goals.Count - achievedCount,
            Currency: prefCurrency);
    }
}
