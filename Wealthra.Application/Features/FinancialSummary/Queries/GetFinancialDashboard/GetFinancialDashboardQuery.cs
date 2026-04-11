using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.FinancialSummary.Models;

namespace Wealthra.Application.Features.FinancialSummary.Queries.GetFinancialDashboard;

public record GetFinancialDashboardQuery : IRequest<FinancialDashboardDto>
{
    public string? TargetCurrency { get; init; }
}

public class GetFinancialDashboardQueryHandler : IRequestHandler<GetFinancialDashboardQuery, FinancialDashboardDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly IIdentityService _identityService;
    private readonly ICurrencyExchangeService _currencyService;

    public GetFinancialDashboardQueryHandler(
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

    public async Task<FinancialDashboardDto> Handle(GetFinancialDashboardQuery request, CancellationToken cancellationToken)
    {
        var targetCurr = request.TargetCurrency?.ToUpperInvariant();
        var cacheKey = targetCurr != null ? $"dashboard_{_currentUserService.UserId}_{targetCurr}" : $"dashboard_{_currentUserService.UserId}";

        // Try to get from cache first
        // Note: For simplicity here, we might invalidate cache differently if we change preferred currency.
        var cachedDashboard = await _cacheService.GetAsync<FinancialDashboardDto>(cacheKey, cancellationToken);
        if (cachedDashboard != null)
        {
            return cachedDashboard;
        }

        var userDetails = await _identityService.GetUserDetailsAsync(_currentUserService.UserId);
        var prefCurrency = targetCurr ?? userDetails?.PreferredCurrency ?? "TRY";

        // Calculate totals dynamically using the currency exchange service
        var incomeGroups = await _context.Incomes
            .Where(i => i.CreatedBy == _currentUserService.UserId)
            .GroupBy(i => i.Currency ?? "TRY")
            .Select(g => new { Currency = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        decimal totalIncome = 0;
        foreach (var group in incomeGroups)
        {
            totalIncome += await _currencyService.ConvertAsync(group.Total, group.Currency, prefCurrency, cancellationToken);
        }

        var expenseGroups = await _context.Expenses
            .Where(e => e.CreatedBy == _currentUserService.UserId)
            .GroupBy(e => e.Currency ?? "TRY")
            .Select(g => new { Currency = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        decimal totalExpenses = 0;
        foreach (var group in expenseGroups)
        {
            totalExpenses += await _currencyService.ConvertAsync(group.Total, group.Currency, prefCurrency, cancellationToken);
        }

        var totalBalance = totalIncome - totalExpenses;

        // Get recent transactions (last 5)
        var recentExpenses = await _context.Expenses
            .Include(e => e.Category)
            .Where(e => e.CreatedBy == _currentUserService.UserId)
            .OrderByDescending(e => e.TransactionDate)
            .Take(5)
            .Select(e => new RecentTransactionDto(
                e.Id,
                "Expense",
                e.Description,
                e.Amount,
                e.Currency ?? "TRY",
                e.TransactionDate,
                e.Category.NameEn))
            .ToListAsync(cancellationToken);

        var recentIncomes = await _context.Incomes
            .Where(i => i.CreatedBy == _currentUserService.UserId)
            .OrderByDescending(i => i.TransactionDate)
            .Take(5)
            .Select(i => new RecentTransactionDto(
                i.Id,
                "Income",
                i.Name,
                i.Amount,
                i.Currency ?? "TRY",
                i.TransactionDate,
                null))
            .ToListAsync(cancellationToken);

        var recentTransactions = recentExpenses
            .Concat(recentIncomes)
            .OrderByDescending(t => t.TransactionDate)
            .Take(5)
            .ToList();

        // Get top spending categories (last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var rawExpenseData = await _context.Expenses
            .Where(e => e.CreatedBy == _currentUserService.UserId && e.TransactionDate >= thirtyDaysAgo)
            .Select(e => new { e.CategoryId, e.Amount, Currency = e.Currency ?? "TRY" })
            .ToListAsync(cancellationToken);

        var convertedExpenses = new List<(int CategoryId, decimal Amount)>();
        foreach (var e in rawExpenseData)
        {
            var amt = await _currencyService.ConvertAsync(e.Amount, e.Currency, prefCurrency, cancellationToken);
            convertedExpenses.Add((e.CategoryId, amt));
        }

        var categoryIds = convertedExpenses.Select(e => e.CategoryId).Distinct().ToList();

        var categoryNames = await _context.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameEn, cancellationToken);

        var topCategories = convertedExpenses
            .GroupBy(e => e.CategoryId)
            .Select(g => new TopCategoryDto(
                categoryNames.GetValueOrDefault(g.Key, "Unknown"),
                g.Sum(e => e.Amount),
                prefCurrency,
                g.Count()))
            .OrderByDescending(c => c.TotalAmount)
            .Take(5)
            .ToList();

        // Get budget alerts (Warning or Exceeded)
        var budgetAlerts = await _context.Budgets
            .Include(b => b.Category)
            .Where(b => b.CreatedBy == _currentUserService.UserId)
            .ToListAsync(cancellationToken);

        var alerts = budgetAlerts
            .Where(b => b.LimitAmount > 0 && (b.CurrentAmount / b.LimitAmount) * 100 >= 80)
            .Select(b =>
            {
                var percentage = (b.CurrentAmount / b.LimitAmount) * 100;
                return new BudgetAlertDto(
                    b.Id,
                    b.Category.NameEn,
                    b.LimitAmount,
                    b.CurrentAmount,
                    b.Currency ?? "TRY",
                    percentage,
                    percentage >= 100 ? "Exceeded" : "Warning");
            })
            .OrderByDescending(a => a.PercentageUsed)
            .ToList();

        // Get unread notifications count
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == _currentUserService.UserId && !n.IsRead)
            .CountAsync(cancellationToken);

        var dashboard = new FinancialDashboardDto(
            totalBalance,
            totalIncome,
            totalExpenses,
            recentTransactions,
            topCategories,
            alerts,
            unreadNotifications,
            prefCurrency);

        // Cache for 5 minutes
        await _cacheService.SetAsync(cacheKey, dashboard, TimeSpan.FromMinutes(5), cancellationToken);

        return dashboard;
    }
}
