using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Application.Features.Expenses.Queries.GetExpenseGeneralInfo;

public record GetExpenseGeneralInfoQuery : IRequest<ExpenseGeneralInfoDto>;

public class GetExpenseGeneralInfoQueryHandler : IRequestHandler<GetExpenseGeneralInfoQuery, ExpenseGeneralInfoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetExpenseGeneralInfoQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ExpenseGeneralInfoDto> Handle(GetExpenseGeneralInfoQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUserService.UserId;

        // Week: Monday to now
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1).Date;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var weeklyTotal = await _context.Expenses
            .Where(e => e.CreatedBy == userId && e.TransactionDate >= startOfWeek)
            .SumAsync(e => e.Amount, cancellationToken);

        var monthlyTotal = await _context.Expenses
            .Where(e => e.CreatedBy == userId && e.TransactionDate >= startOfMonth)
            .SumAsync(e => e.Amount, cancellationToken);

        var yearlyTotal = await _context.Expenses
            .Where(e => e.CreatedBy == userId && e.TransactionDate >= startOfYear)
            .SumAsync(e => e.Amount, cancellationToken);

        // Recurring expenses this month — highlights fixed costs vs one-time spending
        var recurringThisMonth = await _context.Expenses
            .Where(e => e.CreatedBy == userId && e.TransactionDate >= startOfMonth && e.IsRecurring)
            .SumAsync(e => e.Amount, cancellationToken);

        return new ExpenseGeneralInfoDto(
            weeklyTotal,
            monthlyTotal,
            yearlyTotal,
            recurringThisMonth);
    }
}
