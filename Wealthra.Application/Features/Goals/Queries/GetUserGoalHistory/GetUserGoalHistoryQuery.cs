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
    public string? TargetCurrency { get; init; }
}

public class GetUserGoalHistoryQueryHandler : IRequestHandler<GetUserGoalHistoryQuery, PaginatedList<GoalHistoryDto>>
{
    private const string DefaultCurrency = "TRY";
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrencyExchangeService _currencyService;
    private readonly IDisplayCurrencyService _displayCurrencyService;

    public GetUserGoalHistoryQueryHandler(
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

    public async Task<PaginatedList<GoalHistoryDto>> Handle(GetUserGoalHistoryQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Goals
            .Where(g => g.CreatedBy == _currentUserService.UserId)
            .OrderByDescending(g => g.Deadline);

        var paginatedGoals = await PaginatedList<Wealthra.Domain.Entities.Goal>.CreateAsync(
            query,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var targetCurrency = await _displayCurrencyService.GetEffectiveCurrencyAsync(request.TargetCurrency, cancellationToken);
        var dtoList = new List<GoalHistoryDto>();

        foreach (var g in paginatedGoals.Items)
        {
            var source = string.IsNullOrWhiteSpace(g.Currency) ? DefaultCurrency : g.Currency.ToUpperInvariant();
            var targetAmt = g.TargetAmount;
            var currentAmt = g.CurrentAmount;

            if (!string.Equals(source, targetCurrency, StringComparison.Ordinal))
            {
                targetAmt = await _currencyService.ConvertAsync(g.TargetAmount, source, targetCurrency, cancellationToken);
                currentAmt = await _currencyService.ConvertAsync(g.CurrentAmount, source, targetCurrency, cancellationToken);
            }

            dtoList.Add(new GoalHistoryDto(
                g.Id,
                g.Name,
                targetAmt,
                currentAmt,
                targetAmt > 0 ? (currentAmt / targetAmt) * 100 : 0,
                currentAmt > targetAmt ? targetAmt : currentAmt,
                currentAmt >= targetAmt ? 0 : targetAmt - currentAmt,
                g.Deadline,
                currentAmt >= targetAmt));
        }

        return new PaginatedList<GoalHistoryDto>(
            dtoList, 
            paginatedGoals.TotalCount, 
            paginatedGoals.PageNumber, 
            request.PageSize);
    }
}
