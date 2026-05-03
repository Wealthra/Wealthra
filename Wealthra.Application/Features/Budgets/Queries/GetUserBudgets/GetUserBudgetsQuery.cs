using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Budgets.Models;
using Wealthra.Application.Features.Categories.Models;

namespace Wealthra.Application.Features.Budgets.Queries.GetUserBudgets;

public record GetUserBudgetsQuery(
    CategoryDisplayLanguage CategoryLanguage = CategoryDisplayLanguage.English,
    string? TargetCurrency = null) : IRequest<List<BudgetDto>>;

public class GetUserBudgetsQueryHandler : IRequestHandler<GetUserBudgetsQuery, List<BudgetDto>>
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrencyExchangeService _currencyService;
    private readonly IDisplayCurrencyService _displayCurrencyService;

    public GetUserBudgetsQueryHandler(
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

    public async Task<List<BudgetDto>> Handle(GetUserBudgetsQuery request, CancellationToken cancellationToken)
    {
        var useTr = request.CategoryLanguage == CategoryDisplayLanguage.Turkish;
        var targetCurrency = await _displayCurrencyService.GetEffectiveCurrencyAsync(request.TargetCurrency, cancellationToken);

        var budgets = await _context.Budgets
            .Include(b => b.Category)
            .Where(b => b.CreatedBy == _currentUserService.UserId)
            .ToListAsync(cancellationToken);

        var list = new List<BudgetDto>(budgets.Count);
        foreach (var b in budgets)
        {
            var source = string.IsNullOrWhiteSpace(b.Currency) ? DefaultCurrency : b.Currency.ToUpperInvariant();
            var limit = b.LimitAmount;
            var current = b.CurrentAmount;
            if (!string.Equals(source, targetCurrency, StringComparison.Ordinal))
            {
                limit = await _currencyService.ConvertAsync(b.LimitAmount, source, targetCurrency, cancellationToken);
                current = await _currencyService.ConvertAsync(b.CurrentAmount, source, targetCurrency, cancellationToken);
            }

            list.Add(new BudgetDto(
                b.Id,
                limit,
                current,
                limit > 0 ? (current / limit) * 100 : 0,
                GetBudgetStatus(current, limit),
                b.CategoryId,
                useTr ? b.Category.NameTr : b.Category.NameEn,
                targetCurrency));
        }

        return list;
    }

    private static string GetBudgetStatus(decimal currentAmount, decimal limitAmount)
    {
        if (limitAmount == 0) return "Safe";

        var percentage = (currentAmount / limitAmount) * 100;

        return percentage switch
        {
            >= 100 => "Exceeded",
            >= 80 => "Warning",
            _ => "Safe"
        };
    }
}
