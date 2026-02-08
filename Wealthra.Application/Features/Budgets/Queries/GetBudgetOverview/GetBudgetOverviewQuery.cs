using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Budgets.Models;

namespace Wealthra.Application.Features.Budgets.Queries.GetBudgetOverview;

public record GetBudgetOverviewQuery : IRequest<BudgetOverviewDto>;

public class GetBudgetOverviewQueryHandler : IRequestHandler<GetBudgetOverviewQuery, BudgetOverviewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetBudgetOverviewQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<BudgetOverviewDto> Handle(GetBudgetOverviewQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1);
        var currentMonthEnd = currentMonthStart.AddMonths(1);

        var budgets = await _context.Budgets
            .Where(b => b.CreatedBy == _currentUserService.UserId)
            .ToListAsync(cancellationToken);

        // Get expenses for current month
        var currentMonthExpenses = await _context.Expenses
            .Where(e => e.CreatedBy == _currentUserService.UserId &&
                       e.TransactionDate >= currentMonthStart &&
                       e.TransactionDate < currentMonthEnd)
            .SumAsync(e => e.Amount, cancellationToken);

        var totalLimit = budgets.Sum(b => b.LimitAmount);
        var totalSpent = budgets.Sum(b => b.CurrentAmount);
        var percentageUsed = totalLimit > 0 ? (totalSpent / totalLimit) * 100 : 0;

        var budgetsExceeded = budgets.Count(b => b.LimitAmount > 0 && (b.CurrentAmount / b.LimitAmount) * 100 >= 100);
        var budgetsWarning = budgets.Count(b => b.LimitAmount > 0 && (b.CurrentAmount / b.LimitAmount) * 100 >= 80 && (b.CurrentAmount / b.LimitAmount) * 100 < 100);

        var overallStatus = percentageUsed switch
        {
            >= 100 => "Exceeded",
            >= 80 => "Warning",
            _ => "Safe"
        };

        return new BudgetOverviewDto(
            totalLimit,
            totalSpent,
            percentageUsed,
            overallStatus,
            budgets.Count,
            budgetsExceeded,
            budgetsWarning);
    }
}
