using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.FinancialSummary.Models;

namespace Wealthra.Application.Features.FinancialSummary.Queries.GetDashboardWeb;

public record GetDashboardWebQuery : IRequest<DashboardWebDto>
{
    public string? TargetCurrency { get; init; }
}

public class GetDashboardWebQueryHandler : IRequestHandler<GetDashboardWebQuery, DashboardWebDto>
{
    private const int TrendMonthCount = 6;
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly IIdentityService _identityService;
    private readonly ICurrencyExchangeService _currencyService;

    public GetDashboardWebQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        IIdentityService identityService,
        ICurrencyExchangeService currencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _identityService = identityService;
        _currencyService = currencyService;
    }

    public async Task<DashboardWebDto> Handle(GetDashboardWebQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? string.Empty;
        var targetCurr = request.TargetCurrency?.ToUpperInvariant();
        var cacheKey = targetCurr != null ? $"dashboard_web_{userId}_{targetCurr}" : $"dashboard_web_{userId}";
        var cached = await _cacheService.GetAsync<DashboardWebDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var userDetails = await _identityService.GetUserDetailsAsync(userId);
        var prefCurrency = targetCurr
            ?? userDetails?.PreferredCurrency?.ToUpperInvariant()
            ?? DefaultCurrency;

        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEndExclusive = periodStart.AddMonths(1);
        var periodEndDisplay = periodEndExclusive.AddTicks(-1);

        var totalIncomeAllTimeGroups = await _context.Incomes
            .Where(i => i.CreatedBy == userId)
            .GroupBy(i => i.Currency ?? DefaultCurrency)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(x => x.Amount)))
            .ToListAsync(cancellationToken);
        var totalIncomeAllTime = await ConvertGroupedTotalAsync(totalIncomeAllTimeGroups, prefCurrency, cancellationToken);

        var totalExpensesAllTimeGroups = await _context.Expenses
            .Where(e => e.CreatedBy == userId)
            .GroupBy(e => e.Currency ?? DefaultCurrency)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(x => x.Amount)))
            .ToListAsync(cancellationToken);
        var totalExpensesAllTime = await ConvertGroupedTotalAsync(totalExpensesAllTimeGroups, prefCurrency, cancellationToken);

        var totalBalance = totalIncomeAllTime - totalExpensesAllTime;

        var periodIncomeGroups = await _context.Incomes
            .Where(i => i.CreatedBy == userId &&
                        i.TransactionDate >= periodStart &&
                        i.TransactionDate < periodEndExclusive)
            .GroupBy(i => i.Currency ?? DefaultCurrency)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(x => x.Amount)))
            .ToListAsync(cancellationToken);
        var periodIncome = await ConvertGroupedTotalAsync(periodIncomeGroups, prefCurrency, cancellationToken);

        var periodExpensesGroups = await _context.Expenses
            .Where(e => e.CreatedBy == userId &&
                         e.TransactionDate >= periodStart &&
                         e.TransactionDate < periodEndExclusive)
            .GroupBy(e => e.Currency ?? DefaultCurrency)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(x => x.Amount)))
            .ToListAsync(cancellationToken);
        var periodExpenses = await ConvertGroupedTotalAsync(periodExpensesGroups, prefCurrency, cancellationToken);

        var monthlyCashFlow = periodIncome - periodExpenses;
        var savingsRate = periodIncome > 0 ? monthlyCashFlow / periodIncome : 0;

        var activeBudgetsCount = await _context.Budgets
            .CountAsync(b => b.CreatedBy == userId, cancellationToken);

        var goalsAgg = await _context.Goals
            .Where(g => g.CreatedBy == userId)
            .Select(g => new
            {
                g.TargetAmount,
                g.CurrentAmount,
                Currency = g.Currency ?? DefaultCurrency,
                IsCompleted = g.CurrentAmount >= g.TargetAmount
            })
            .ToListAsync(cancellationToken);

        var totalGoals = goalsAgg.Count;
        var achievedGoals = goalsAgg.Count(g => g.IsCompleted);
        var goalsCurrent = 0m;
        var goalsTarget = 0m;
        foreach (var goal in goalsAgg)
        {
            goalsCurrent += await _currencyService.ConvertAsync(goal.CurrentAmount, goal.Currency, prefCurrency, cancellationToken);
            goalsTarget += await _currencyService.ConvertAsync(goal.TargetAmount, goal.Currency, prefCurrency, cancellationToken);
        }

        var unreadNotifications = await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        var trendStart = periodStart.AddMonths(-(TrendMonthCount - 1));
        var trendIncomes = await _context.Incomes
            .Where(i => i.CreatedBy == userId &&
                        i.TransactionDate >= trendStart &&
                        i.TransactionDate < periodEndExclusive)
            .Select(i => new TransactionAmount(i.TransactionDate, i.Amount, i.Currency ?? DefaultCurrency))
            .ToListAsync(cancellationToken);

        var trendExpenses = await _context.Expenses
            .Where(e => e.CreatedBy == userId &&
                        e.TransactionDate >= trendStart &&
                        e.TransactionDate < periodEndExclusive)
            .Select(e => new TransactionAmount(e.TransactionDate, e.Amount, e.Currency ?? DefaultCurrency))
            .ToListAsync(cancellationToken);

        var trendPoints = new List<DashboardWebIncomeExpensePointDto>();
        for (var monthStart = trendStart; monthStart < periodEndExclusive; monthStart = monthStart.AddMonths(1))
        {
            var next = monthStart.AddMonths(1);
            var incomeGroups = trendIncomes
                .Where(i => i.TransactionDate >= monthStart && i.TransactionDate < next)
                .GroupBy(i => i.Currency)
                .Select(g => new CurrencyTotal(g.Key, g.Sum(x => x.Amount)))
                .ToList();
            var income = await ConvertGroupedTotalAsync(incomeGroups, prefCurrency, cancellationToken);

            var expenseGroups = trendExpenses
                .Where(e => e.TransactionDate >= monthStart && e.TransactionDate < next)
                .GroupBy(e => e.Currency)
                .Select(g => new CurrencyTotal(g.Key, g.Sum(x => x.Amount)))
                .ToList();
            var expense = await ConvertGroupedTotalAsync(expenseGroups, prefCurrency, cancellationToken);
            var label = monthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture);
            trendPoints.Add(new DashboardWebIncomeExpensePointDto(label, income, expense));
        }

        var periodExpenseRows = await _context.Expenses
            .Where(e => e.CreatedBy == userId &&
                         e.TransactionDate >= periodStart &&
                         e.TransactionDate < periodEndExclusive)
            .Select(e => new ExpenseCategoryAmount(e.CategoryId, e.Category.NameEn, e.Amount, e.Currency ?? DefaultCurrency))
            .ToListAsync(cancellationToken);

        var convertedPeriodExpenseRows = new List<(int CategoryId, string CategoryName, decimal Amount)>();
        foreach (var row in periodExpenseRows)
        {
            var convertedAmount = await _currencyService.ConvertAsync(row.Amount, row.Currency, prefCurrency, cancellationToken);
            convertedPeriodExpenseRows.Add((row.CategoryId, row.CategoryName, convertedAmount));
        }

        var spendTotal = convertedPeriodExpenseRows.Sum(e => e.Amount);
        var breakdownCategories = convertedPeriodExpenseRows
            .GroupBy(e => new { e.CategoryId, e.CategoryName })
            .Select(g =>
            {
                var amount = g.Sum(x => x.Amount);
                var pct = spendTotal > 0 ? amount / spendTotal : 0;
                return new DashboardWebSpendingCategoryDto(
                    g.Key.CategoryName,
                    amount,
                    g.Count(),
                    pct);
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

        var topSpendingCategories = breakdownCategories
            .Take(5)
            .Select(c => new DashboardWebTopCategoryDto(c.CategoryName, c.TotalAmount, c.TransactionCount))
            .ToList();

        var recentExpenseRows = await _context.Expenses
            .Where(e => e.CreatedBy == userId)
            .OrderByDescending(e => e.TransactionDate)
            .Take(5)
            .Select(e => new RecentTransactionAmount(
                e.Id,
                "Expense",
                e.Description,
                e.Amount,
                e.Currency ?? DefaultCurrency,
                e.TransactionDate,
                e.Category.NameEn,
                e.IsRecurring,
                null))
            .ToListAsync(cancellationToken);

        var recentIncomeRows = await _context.Incomes
            .Where(i => i.CreatedBy == userId)
            .OrderByDescending(i => i.TransactionDate)
            .Take(5)
            .Select(i => new RecentTransactionAmount(
                i.Id,
                "Income",
                i.Name,
                i.Amount,
                i.Currency ?? DefaultCurrency,
                i.TransactionDate,
                null,
                i.IsRecurring,
                null))
            .ToListAsync(cancellationToken);

        var recentTransactions = recentExpenseRows
            .Concat(recentIncomeRows)
            .OrderByDescending(t => t.TransactionDate)
            .Take(5)
            .ToList();

        var normalizedRecentTransactions = new List<DashboardWebRecentTransactionDto>(recentTransactions.Count);
        foreach (var transaction in recentTransactions)
        {
            var convertedAmount = await _currencyService.ConvertAsync(transaction.Amount, transaction.Currency, prefCurrency, cancellationToken);
            normalizedRecentTransactions.Add(new DashboardWebRecentTransactionDto(
                transaction.Id,
                transaction.Type,
                transaction.Description,
                convertedAmount,
                transaction.TransactionDate,
                transaction.CategoryName,
                transaction.IsRecurring,
                transaction.MerchantName));
        }

        var budgetEntities = await _context.Budgets
            .Include(b => b.Category)
            .Where(b => b.CreatedBy == userId)
            .Select(b => new BudgetAmount(
                b.Id,
                b.Category.NameEn,
                b.LimitAmount,
                b.CurrentAmount,
                b.Currency ?? DefaultCurrency))
            .ToListAsync(cancellationToken);

        var budgetAlerts = new List<DashboardWebBudgetAlertDto>();
        foreach (var budget in budgetEntities.Where(b => b.LimitAmount > 0))
        {
            var convertedLimit = await _currencyService.ConvertAsync(budget.LimitAmount, budget.Currency, prefCurrency, cancellationToken);
            var convertedCurrent = await _currencyService.ConvertAsync(budget.CurrentAmount, budget.Currency, prefCurrency, cancellationToken);
            var fraction = convertedLimit == 0 ? 0 : convertedCurrent / convertedLimit;
            if (fraction < 0.8m)
            {
                continue;
            }

            var status = fraction >= 1m ? "Exceeded" : "Warning";
            budgetAlerts.Add(new DashboardWebBudgetAlertDto(
                budget.Id,
                budget.CategoryName,
                convertedLimit,
                convertedCurrent,
                fraction,
                status));
        }

        budgetAlerts = budgetAlerts
            .OrderByDescending(a => a.PercentageUsed)
            .ToList();

        var recommendations = BuildRecommendations(budgetAlerts, prefCurrency);

        var summary = new DashboardWebSummaryDto(
            totalBalance,
            periodIncome,
            periodExpenses,
            periodStart,
            periodEndDisplay,
            monthlyCashFlow,
            savingsRate,
            activeBudgetsCount,
            new DashboardWebGoalsCountDto(totalGoals, achievedGoals),
            unreadNotifications,
            prefCurrency);

        var charts = new DashboardWebChartsDto(
            new DashboardWebIncomeExpenseTrendDto("month", trendPoints),
            new DashboardWebSpendingsBreakdownDto("category", spendTotal, breakdownCategories));

        var lists = new DashboardWebListsDto(
            normalizedRecentTransactions,
            topSpendingCategories,
            budgetAlerts);

        var goalsOverview = new DashboardWebGoalsOverviewDto(
            totalGoals,
            achievedGoals,
            goalsCurrent,
            goalsTarget,
            prefCurrency);

        var dto = new DashboardWebDto(summary, charts, lists, goalsOverview, recommendations);

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
        return dto;
    }

    private async Task<decimal> ConvertGroupedTotalAsync(
        IEnumerable<CurrencyTotal> groupedRows,
        string targetCurrency,
        CancellationToken cancellationToken)
    {
        var total = 0m;
        foreach (var row in groupedRows)
        {
            total += await _currencyService.ConvertAsync(row.Total, row.Currency, targetCurrency, cancellationToken);
        }

        return total;
    }

    private static List<DashboardWebRecommendationDto> BuildRecommendations(
        List<DashboardWebBudgetAlertDto> alerts,
        string currency)
    {
        var list = new List<DashboardWebRecommendationDto>();
        foreach (var a in alerts.Take(10))
        {
            var over = a.CurrentAmount - a.LimitAmount;
            var title = a.Status == "Exceeded"
                ? $"You exceeded your {a.CategoryName} budget"
                : $"You're close to your {a.CategoryName} budget";

            string description;
            if (a.Status == "Exceeded" && over > 0)
            {
                description = $"You are {over:F0} {currency} over your limit. Consider reducing {a.CategoryName} spend next month.";
            }
            else
            {
                var room = a.LimitAmount - a.CurrentAmount;
                description = room >= 0
                    ? $"Leave about {room:F0} {currency} in this category to stay within your limit."
                    : "Review this category to avoid overspending.";
            }

            var severity = a.Status == "Exceeded" ? "high" : "medium";
            list.Add(new DashboardWebRecommendationDto(
                $"budget_{a.BudgetId}",
                "spending_insight",
                title,
                description,
                a.CategoryName,
                severity));
        }

        return list;
    }

    private sealed record CurrencyTotal(string Currency, decimal Total);
    private sealed record TransactionAmount(DateTime TransactionDate, decimal Amount, string Currency);
    private sealed record ExpenseCategoryAmount(int CategoryId, string CategoryName, decimal Amount, string Currency);
    private sealed record RecentTransactionAmount(
        int Id,
        string Type,
        string Description,
        decimal Amount,
        string Currency,
        DateTime TransactionDate,
        string? CategoryName,
        bool IsRecurring,
        string? MerchantName);
    private sealed record BudgetAmount(int Id, string CategoryName, decimal LimitAmount, decimal CurrentAmount, string Currency);
}
