using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.FinancialSummary.Models;

namespace Wealthra.Application.Features.FinancialSummary.Queries.GetFinancialDashboard;

public record GetFinancialDashboardQuery : IRequest<FinancialDashboardDto>;

public class GetFinancialDashboardQueryHandler : IRequestHandler<GetFinancialDashboardQuery, FinancialDashboardDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetFinancialDashboardQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<FinancialDashboardDto> Handle(GetFinancialDashboardQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"dashboard_{_currentUserService.UserId}";

        // Try to get from cache first
        var cachedDashboard = await _cacheService.GetAsync<FinancialDashboardDto>(cacheKey, cancellationToken);
        if (cachedDashboard != null)
        {
            return cachedDashboard;
        }

        // Calculate totals
        var totalIncome = await _context.Incomes
            .Where(i => i.CreatedBy == _currentUserService.UserId)
            .SumAsync(i => i.Amount, cancellationToken);

        var totalExpenses = await _context.Expenses
            .Where(e => e.CreatedBy == _currentUserService.UserId)
            .SumAsync(e => e.Amount, cancellationToken);

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
                e.TransactionDate,
                e.Category.Name))
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
        var topCategories = await _context.Expenses
            .Include(e => e.Category)
            .Where(e => e.CreatedBy == _currentUserService.UserId && e.TransactionDate >= thirtyDaysAgo)
            .GroupBy(e => new { e.CategoryId, e.Category.Name })
            .Select(g => new TopCategoryDto(
                g.Key.Name,
                g.Sum(e => e.Amount),
                g.Count()))
            .OrderByDescending(c => c.TotalAmount)
            .Take(5)
            .ToListAsync(cancellationToken);

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
                    b.Category.Name,
                    b.LimitAmount,
                    b.CurrentAmount,
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
            unreadNotifications);

        // Cache for 5 minutes
        await _cacheService.SetAsync(cacheKey, dashboard, TimeSpan.FromMinutes(5), cancellationToken);

        return dashboard;
    }
}
