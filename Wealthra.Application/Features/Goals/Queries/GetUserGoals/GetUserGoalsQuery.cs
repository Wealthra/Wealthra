using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Goals.Models;

namespace Wealthra.Application.Features.Goals.Queries.GetUserGoals;

public record GetUserGoalsQuery(string? TargetCurrency = null) : IRequest<List<GoalDto>>;

public class GetUserGoalsQueryHandler : IRequestHandler<GetUserGoalsQuery, List<GoalDto>>
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrencyExchangeService _currencyService;
    private readonly IDisplayCurrencyService _displayCurrencyService;

    public GetUserGoalsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICurrencyExchangeService currencyService,
        IDisplayCurrencyService displayCurrencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _currencyService = currencyService;
        _displayCurrencyService = displayCurrencyService;
    }

    public async Task<List<GoalDto>> Handle(GetUserGoalsQuery request, CancellationToken cancellationToken)
    {
        var goals = await _context.Goals
            .Where(g => g.CreatedBy == _currentUserService.UserId)
            .OrderBy(g => g.Deadline)
            .ToListAsync(cancellationToken);

        var targetCurrency = await _displayCurrencyService.GetEffectiveCurrencyAsync(request.TargetCurrency, cancellationToken);
        var list = new List<GoalDto>(goals.Count);
        foreach (var g in goals)
        {
            var source = string.IsNullOrWhiteSpace(g.Currency) ? DefaultCurrency : g.Currency.ToUpperInvariant();
            var targetAmt = g.TargetAmount;
            var currentAmt = g.CurrentAmount;
            if (!string.Equals(source, targetCurrency, StringComparison.Ordinal))
            {
                targetAmt = await _currencyService.ConvertAsync(g.TargetAmount, source, targetCurrency, cancellationToken);
                currentAmt = await _currencyService.ConvertAsync(g.CurrentAmount, source, targetCurrency, cancellationToken);
            }

            list.Add(new GoalDto(
                g.Id,
                g.Name,
                targetAmt,
                currentAmt,
                targetAmt > 0 ? (currentAmt / targetAmt) * 100 : 0,
                g.Deadline,
                g.CurrentAmount >= g.TargetAmount,
                targetCurrency));
        }

        return list;
    }
}
