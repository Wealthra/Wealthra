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
    private const string DefaultCurrency = "TRY";

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

        var userDetails = await _identityService.GetUserDetailsAsync(_currentUserService.UserId!);
        var prefCurrency = targetCurr
            ?? userDetails?.PreferredCurrency?.ToUpperInvariant()
            ?? DefaultCurrency;

        // Calculate totals dynamically using the currency exchange service
        var incomeGroups = await _context.Incomes
            .Where(i => i.CreatedBy == _currentUserService.UserId)
            .GroupBy(i => i.Currency ?? DefaultCurrency)
            .Select(g => new { Currency = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        decimal totalIncome = 0;
        foreach (var group in incomeGroups)
        {
            totalIncome += await _currencyService.ConvertAsync(group.Total, group.Currency, prefCurrency, cancellationToken);
        }

        var expenseGroups = await _context.Expenses
            .Where(e => e.CreatedBy == _currentUserService.UserId)
            .GroupBy(e => e.Currency ?? DefaultCurrency)
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
            .Select(e => new
            {
                e.Id,
                Type = "Expense",
                Description = e.Description,
                e.Amount,
                Currency = e.Currency ?? DefaultCurrency,
                e.TransactionDate,
                CategoryName = (string?)e.Category.NameEn
            })
            .ToListAsync(cancellationToken);

        var recentIncomes = await _context.Incomes
            .Where(i => i.CreatedBy == _currentUserService.UserId)
            .OrderByDescending(i => i.TransactionDate)
            .Take(5)
            .Select(i => new
            {
                i.Id,
                Type = "Income",
                Description = i.Name,
                i.Amount,
                Currency = i.Currency ?? DefaultCurrency,
                i.TransactionDate,
                CategoryName = (string?)null
            })
            .ToListAsync(cancellationToken);

        var recentTransactionsRaw = recentExpenses
            .Concat(recentIncomes)
            .OrderByDescending(t => t.TransactionDate)
            .Take(5)
            .ToList();

        var recentTransactions = new List<RecentTransactionDto>(recentTransactionsRaw.Count);
        foreach (var transaction in recentTransactionsRaw)
        {
            var convertedAmount = await _currencyService.ConvertAsync(
                transaction.Amount,
                transaction.Currency,
                prefCurrency,
                cancellationToken);

            recentTransactions.Add(new RecentTransactionDto(
                transaction.Id,
                transaction.Type,
                transaction.Description,
                convertedAmount,
                prefCurrency,
                transaction.TransactionDate,
                transaction.CategoryName));
        }

        // Get top spending categories (last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var rawExpenseData = await _context.Expenses
            .Where(e => e.CreatedBy == _currentUserService.UserId && e.TransactionDate >= thirtyDaysAgo)
            .Select(e => new { e.CategoryId, e.Amount, Currency = e.Currency ?? DefaultCurrency })
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
            .Select(b => new
            {
                b.Id,
                CategoryName = b.Category.NameEn,
                b.LimitAmount,
                b.CurrentAmount,
                Currency = b.Currency ?? DefaultCurrency
            })
            .ToListAsync(cancellationToken);

        var alerts = new List<BudgetAlertDto>();
        foreach (var budget in budgetAlerts.Where(b => b.LimitAmount > 0))
        {
            var convertedLimit = await _currencyService.ConvertAsync(
                budget.LimitAmount,
                budget.Currency,
                prefCurrency,
                cancellationToken);
            var convertedCurrent = await _currencyService.ConvertAsync(
                budget.CurrentAmount,
                budget.Currency,
                prefCurrency,
                cancellationToken);

            var percentage = convertedLimit == 0 ? 0 : (convertedCurrent / convertedLimit) * 100;
            if (percentage < 80)
            {
                continue;
            }

            alerts.Add(new BudgetAlertDto(
                budget.Id,
                budget.CategoryName,
                convertedLimit,
                convertedCurrent,
                prefCurrency,
                percentage,
                percentage >= 100 ? "Exceeded" : "Warning"));
        }

        alerts = alerts
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
