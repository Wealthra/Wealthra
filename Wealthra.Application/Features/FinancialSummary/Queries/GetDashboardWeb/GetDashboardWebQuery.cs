using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.FinancialSummary.Models;

namespace Wealthra.Application.Features.FinancialSummary.Queries.GetDashboardWeb;

public record GetDashboardWebQuery : IRequest<DashboardWebDto>;

public class GetDashboardWebQueryHandler : IRequestHandler<GetDashboardWebQuery, DashboardWebDto>
{
    private const int TrendMonthCount = 6;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetDashboardWebQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<DashboardWebDto> Handle(GetDashboardWebQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var cacheKey = $"dashboard_web_{userId}";
        var cached = await _cacheService.GetAsync<DashboardWebDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEndExclusive = periodStart.AddMonths(1);
        var periodEndDisplay = periodEndExclusive.AddTicks(-1);

        var totalIncomeAllTime = await _context.Incomes
            .Where(i => i.CreatedBy == userId)
            .SumAsync(i => i.Amount, cancellationToken);

        var totalExpensesAllTime = await _context.Expenses
            .Where(e => e.CreatedBy == userId)
            .SumAsync(e => e.Amount, cancellationToken);

        var totalBalance = totalIncomeAllTime - totalExpensesAllTime;

        var periodIncome = await _context.Incomes
            .Where(i => i.CreatedBy == userId &&
                        i.TransactionDate >= periodStart &&
                        i.TransactionDate < periodEndExclusive)
            .SumAsync(i => i.Amount, cancellationToken);

        var periodExpenses = await _context.Expenses
            .Where(e => e.CreatedBy == userId &&
                         e.TransactionDate >= periodStart &&
                         e.TransactionDate < periodEndExclusive)
            .SumAsync(e => e.Amount, cancellationToken);

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
                IsCompleted = g.CurrentAmount >= g.TargetAmount
            })
            .ToListAsync(cancellationToken);

        var totalGoals = goalsAgg.Count;
        var achievedGoals = goalsAgg.Count(g => g.IsCompleted);
        var goalsCurrent = goalsAgg.Sum(g => g.CurrentAmount);
        var goalsTarget = goalsAgg.Sum(g => g.TargetAmount);

        var unreadNotifications = await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        var trendStart = periodStart.AddMonths(-(TrendMonthCount - 1));
        var trendIncomes = await _context.Incomes
            .Where(i => i.CreatedBy == userId &&
                        i.TransactionDate >= trendStart &&
                        i.TransactionDate < periodEndExclusive)
            .Select(i => new { i.TransactionDate, i.Amount })
            .ToListAsync(cancellationToken);

        var trendExpenses = await _context.Expenses
            .Where(e => e.CreatedBy == userId &&
                        e.TransactionDate >= trendStart &&
                        e.TransactionDate < periodEndExclusive)
            .Select(e => new { e.TransactionDate, e.Amount })
            .ToListAsync(cancellationToken);

        var trendPoints = new List<DashboardWebIncomeExpensePointDto>();
        for (var monthStart = trendStart; monthStart < periodEndExclusive; monthStart = monthStart.AddMonths(1))
        {
            var next = monthStart.AddMonths(1);
            var income = trendIncomes
                .Where(i => i.TransactionDate >= monthStart && i.TransactionDate < next)
                .Sum(i => i.Amount);
            var expense = trendExpenses
                .Where(e => e.TransactionDate >= monthStart && e.TransactionDate < next)
                .Sum(e => e.Amount);
            var label = monthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture);
            trendPoints.Add(new DashboardWebIncomeExpensePointDto(label, income, expense));
        }

        var periodExpenseRows = await _context.Expenses
            .Where(e => e.CreatedBy == userId &&
                         e.TransactionDate >= periodStart &&
                         e.TransactionDate < periodEndExclusive)
            .Select(e => new { e.CategoryId, e.Amount, CategoryName = e.Category.NameEn })
            .ToListAsync(cancellationToken);

        var spendTotal = periodExpenseRows.Sum(e => e.Amount);
        var breakdownCategories = periodExpenseRows
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
            .Select(e => new DashboardWebRecentTransactionDto(
                e.Id,
                "Expense",
                e.Description,
                e.Amount,
                e.TransactionDate,
                e.Category.NameEn,
                e.IsRecurring,
                (string?)null))
            .ToListAsync(cancellationToken);

        var recentIncomeRows = await _context.Incomes
            .Where(i => i.CreatedBy == userId)
            .OrderByDescending(i => i.TransactionDate)
            .Take(5)
            .Select(i => new DashboardWebRecentTransactionDto(
                i.Id,
                "Income",
                i.Name,
                i.Amount,
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

        var budgetEntities = await _context.Budgets
            .Include(b => b.Category)
            .Where(b => b.CreatedBy == userId)
            .ToListAsync(cancellationToken);

        var budgetAlerts = budgetEntities
            .Where(b => b.LimitAmount > 0)
            .Select(b =>
            {
                var fraction = b.CurrentAmount / b.LimitAmount;
                return new { Budget = b, Fraction = fraction };
            })
            .Where(x => x.Fraction >= 0.8m)
            .Select(x =>
            {
                var b = x.Budget;
                var fraction = x.Fraction;
                var status = fraction >= 1m ? "Exceeded" : "Warning";
                return new DashboardWebBudgetAlertDto(
                    b.Id,
                    b.Category.NameEn,
                    b.LimitAmount,
                    b.CurrentAmount,
                    fraction,
                    status);
            })
            .OrderByDescending(a => a.PercentageUsed)
            .ToList();

        var recommendations = BuildRecommendations(budgetAlerts);

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
            unreadNotifications);

        var charts = new DashboardWebChartsDto(
            new DashboardWebIncomeExpenseTrendDto("month", trendPoints),
            new DashboardWebSpendingsBreakdownDto("category", spendTotal, breakdownCategories));

        var lists = new DashboardWebListsDto(
            recentTransactions,
            topSpendingCategories,
            budgetAlerts);

        var goalsOverview = new DashboardWebGoalsOverviewDto(
            totalGoals,
            achievedGoals,
            goalsCurrent,
            goalsTarget);

        var dto = new DashboardWebDto(summary, charts, lists, goalsOverview, recommendations);

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
        return dto;
    }

    private static List<DashboardWebRecommendationDto> BuildRecommendations(
        List<DashboardWebBudgetAlertDto> alerts)
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
                description = $"You are ${over:F0} over your limit. Consider reducing {a.CategoryName} spend next month.";
            }
            else
            {
                var room = a.LimitAmount - a.CurrentAmount;
                description = room >= 0
                    ? $"Leave about ${room:F0} in this category to stay within your limit."
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
}
