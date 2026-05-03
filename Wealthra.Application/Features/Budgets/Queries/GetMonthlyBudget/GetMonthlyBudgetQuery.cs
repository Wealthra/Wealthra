using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Budgets.Models;
using Wealthra.Application.Features.Categories.Models;

namespace Wealthra.Application.Features.Budgets.Queries.GetMonthlyBudget;

public record GetMonthlyBudgetQuery(
    CategoryDisplayLanguage CategoryLanguage = CategoryDisplayLanguage.English,
    string? TargetCurrency = null) : IRequest<MonthlyBudgetSummaryDto>;

public class GetMonthlyBudgetQueryHandler : IRequestHandler<GetMonthlyBudgetQuery, MonthlyBudgetSummaryDto>
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrencyExchangeService _currencyService;
    private readonly IDisplayCurrencyService _displayCurrencyService;

    public GetMonthlyBudgetQueryHandler(
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

    public async Task<MonthlyBudgetSummaryDto> Handle(GetMonthlyBudgetQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUserService.UserId;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfNextMonth = startOfMonth.AddMonths(1);
        var useTr = request.CategoryLanguage == CategoryDisplayLanguage.Turkish;
        var effective = await _displayCurrencyService.GetEffectiveCurrencyAsync(request.TargetCurrency, cancellationToken);

        var budgets = await _context.Budgets
            .Include(b => b.Category)
            .Where(b => b.CreatedBy == userId)
            .ToListAsync(cancellationToken);

        if (budgets.Count == 0)
        {
            return new MonthlyBudgetSummaryDto(0, 0, 0, 0, "Safe", 0, 0, 0, [], effective);
        }

        var budgetedCategoryIds = budgets.Select(b => b.CategoryId).ToList();

        var spendingGroups = await _context.Expenses
            .Where(e => e.CreatedBy == userId
                     && e.TransactionDate >= startOfMonth
                     && e.TransactionDate < startOfNextMonth
                     && budgetedCategoryIds.Contains(e.CategoryId))
            .GroupBy(e => new { e.CategoryId, Curr = e.Currency ?? DefaultCurrency })
            .Select(g => new { g.Key.CategoryId, Currency = g.Key.Curr, Spent = g.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);

        var spentByCategory = new Dictionary<int, decimal>();
        foreach (var g in spendingGroups)
        {
            var source = string.IsNullOrWhiteSpace(g.Currency) ? DefaultCurrency : g.Currency.ToUpperInvariant();
            var amt = g.Spent;
            if (!string.Equals(source, effective, StringComparison.Ordinal))
            {
                amt = await _currencyService.ConvertAsync(g.Spent, source, effective, cancellationToken);
            }

            spentByCategory[g.CategoryId] = spentByCategory.GetValueOrDefault(g.CategoryId) + amt;
        }

        var categoryBreakdown = new List<MonthlyBudgetCategoryDto>();
        foreach (var b in budgets)
        {
            var source = string.IsNullOrWhiteSpace(b.Currency) ? DefaultCurrency : b.Currency.ToUpperInvariant();
            var limit = b.LimitAmount;
            if (!string.Equals(source, effective, StringComparison.Ordinal))
            {
                limit = await _currencyService.ConvertAsync(b.LimitAmount, source, effective, cancellationToken);
            }

            var spent = spentByCategory.GetValueOrDefault(b.CategoryId, 0m);
            var remaining = limit - spent;
            var percentage = limit > 0 ? Math.Round((spent / limit) * 100, 2) : 0;
            var status = percentage switch
            {
                >= 100 => "Exceeded",
                >= 80 => "Warning",
                _ => "Safe"
            };
            var name = useTr ? b.Category.NameTr : b.Category.NameEn;
            categoryBreakdown.Add(new MonthlyBudgetCategoryDto(
                b.Id, name, limit, spent,
                remaining, percentage, status));
        }

        categoryBreakdown = categoryBreakdown.OrderByDescending(c => c.PercentageUsed).ToList();

        var totalLimit = categoryBreakdown.Sum(c => c.LimitAmount);
        var totalSpent = categoryBreakdown.Sum(c => c.SpentThisMonth);
        var totalRemaining = totalLimit - totalSpent;
        var overallPercentage = totalLimit > 0 ? Math.Round((totalSpent / totalLimit) * 100, 2) : 0;
        var overallStatus = overallPercentage switch
        {
            >= 100 => "Exceeded",
            >= 80 => "Warning",
            _ => "Safe"
        };

        return new MonthlyBudgetSummaryDto(
            totalLimit,
            totalSpent,
            totalRemaining,
            overallPercentage,
            overallStatus,
            categoryBreakdown.Count,
            categoryBreakdown.Count(c => c.Status == "Exceeded"),
            categoryBreakdown.Count(c => c.Status == "Warning"),
            categoryBreakdown,
            effective);
    }
}
