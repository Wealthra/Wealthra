using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Budgets.Models;

namespace Wealthra.Application.Features.Budgets.Queries.GetMonthlyBudget;

public record GetMonthlyBudgetQuery : IRequest<MonthlyBudgetSummaryDto>;

public class GetMonthlyBudgetQueryHandler : IRequestHandler<GetMonthlyBudgetQuery, MonthlyBudgetSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMonthlyBudgetQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<MonthlyBudgetSummaryDto> Handle(GetMonthlyBudgetQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUserService.UserId;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfNextMonth = startOfMonth.AddMonths(1);

        // 1. Load all user budgets with category names
        var budgets = await _context.Budgets
            .Where(b => b.CreatedBy == userId)
            .Select(b => new { b.Id, b.CategoryId, b.LimitAmount, b.Category.NameEn, b.Category.NameTr })
            .ToListAsync(cancellationToken);

        if (budgets.Count == 0)
        {
            return new MonthlyBudgetSummaryDto(0, 0, 0, 0, "Safe", 0, 0, 0, []);
        }

        // 2. Get this month's actual spending per category (only for categories that have a budget)
        var budgetedCategoryIds = budgets.Select(b => b.CategoryId).ToList();

        var monthlySpendingPerCategory = await _context.Expenses
            .Where(e => e.CreatedBy == userId
                     && e.TransactionDate >= startOfMonth
                     && e.TransactionDate < startOfNextMonth
                     && budgetedCategoryIds.Contains(e.CategoryId))
            .GroupBy(e => e.CategoryId)
            .Select(g => new { CategoryId = g.Key, Spent = g.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);

        var spendingLookup = monthlySpendingPerCategory.ToDictionary(s => s.CategoryId, s => s.Spent);

        // 3. Build per-category breakdown
        var categoryBreakdown = budgets
            .Select(b =>
            {
                var spent = spendingLookup.GetValueOrDefault(b.CategoryId, 0m);
                var remaining = b.LimitAmount - spent;
                var percentage = b.LimitAmount > 0 ? Math.Round((spent / b.LimitAmount) * 100, 2) : 0;
                var status = percentage switch
                {
                    >= 100 => "Exceeded",
                    >= 80  => "Warning",
                    _      => "Safe"
                };
                return new MonthlyBudgetCategoryDto(
                    b.Id, b.NameEn, b.NameTr, b.LimitAmount, spent,
                    remaining, percentage, status);
            })
            .OrderByDescending(c => c.PercentageUsed)
            .ToList();

        // 4. Overall totals
        var totalLimit = categoryBreakdown.Sum(c => c.LimitAmount);
        var totalSpent = categoryBreakdown.Sum(c => c.SpentThisMonth);
        var totalRemaining = totalLimit - totalSpent;
        var overallPercentage = totalLimit > 0 ? Math.Round((totalSpent / totalLimit) * 100, 2) : 0;
        var overallStatus = overallPercentage switch
        {
            >= 100 => "Exceeded",
            >= 80  => "Warning",
            _      => "Safe"
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
            categoryBreakdown);
    }
}
