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

    public GetUserBudgetsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICurrencyExchangeService currencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _currencyService = currencyService;
    }

    public async Task<List<BudgetDto>> Handle(GetUserBudgetsQuery request, CancellationToken cancellationToken)
    {
        var useTr = request.CategoryLanguage == CategoryDisplayLanguage.Turkish;
        var targetCurrency = request.TargetCurrency?.Trim();

        if (string.IsNullOrEmpty(targetCurrency))
        {
            return await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.CreatedBy == _currentUserService.UserId)
                .Select(b => new BudgetDto(
                    b.Id,
                    b.LimitAmount,
                    b.CurrentAmount,
                    b.LimitAmount > 0 ? (b.CurrentAmount / b.LimitAmount) * 100 : 0,
                    GetBudgetStatus(b.CurrentAmount, b.LimitAmount),
                    b.CategoryId,
                    useTr ? b.Category.NameTr : b.Category.NameEn,
                    b.Currency ?? DefaultCurrency))
                .ToListAsync(cancellationToken);
        }

        targetCurrency = targetCurrency.ToUpperInvariant();
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
