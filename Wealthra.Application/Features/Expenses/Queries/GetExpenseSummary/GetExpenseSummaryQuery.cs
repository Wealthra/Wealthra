using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Application.Features.Expenses.Queries.GetExpenseSummary;

public record GetExpenseSummaryQuery : IRequest<List<ExpenseSummaryDto>>
{
    public string Period { get; init; } = "Monthly";
}

public class GetExpenseSummaryQueryValidator : AbstractValidator<GetExpenseSummaryQuery>
{
    public GetExpenseSummaryQueryValidator()
    {
        RuleFor(v => v.Period)
            .Must(p => p == "Weekly" || p == "Monthly" || p == "Yearly")
            .WithMessage("Period must be Weekly, Monthly, or Yearly.");
    }
}

public class GetExpenseSummaryQueryHandler : IRequestHandler<GetExpenseSummaryQuery, List<ExpenseSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetExpenseSummaryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ExpenseSummaryDto>> Handle(GetExpenseSummaryQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expenses = await _context.Expenses
            .Include(e => e.Category)
            .Where(e => e.CreatedBy == _currentUserService.UserId)
            .ToListAsync(cancellationToken);

        var groupedExpenses = request.Period switch
        {
            "Weekly" => GroupByWeek(expenses, now),
            "Monthly" => GroupByMonth(expenses, now),
            "Yearly" => GroupByYear(expenses, now),
            _ => GroupByMonth(expenses, now)
        };

        return groupedExpenses;
    }

    private List<ExpenseSummaryDto> GroupByWeek(List<Domain.Entities.Expense> expenses, DateTime now)
    {
        var last12Weeks = Enumerable.Range(0, 12)
            .Select(i => now.AddDays(-i * 7).Date)
            .Reverse()
            .ToList();

        return last12Weeks.Select(weekStart =>
        {
            var weekEnd = weekStart.AddDays(7);
            var weekExpenses = expenses.Where(e => e.TransactionDate >= weekStart && e.TransactionDate < weekEnd).ToList();

            return new ExpenseSummaryDto(
                $"Week of {weekStart:MMM dd}",
                weekExpenses.Sum(e => e.Amount),
                weekExpenses.Count,
                weekExpenses.GroupBy(e => e.Category.Name)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount))
            );
        }).ToList();
    }

    private List<ExpenseSummaryDto> GroupByMonth(List<Domain.Entities.Expense> expenses, DateTime now)
    {
        var last12Months = Enumerable.Range(0, 12)
            .Select(i => now.AddMonths(-i).Date)
            .Reverse()
            .ToList();

        return last12Months.Select(monthStart =>
        {
            var monthExpenses = expenses.Where(e =>
                e.TransactionDate.Year == monthStart.Year &&
                e.TransactionDate.Month == monthStart.Month).ToList();

            return new ExpenseSummaryDto(
                monthStart.ToString("MMMM yyyy"),
                monthExpenses.Sum(e => e.Amount),
                monthExpenses.Count,
                monthExpenses.GroupBy(e => e.Category.Name)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount))
            );
        }).ToList();
    }

    private List<ExpenseSummaryDto> GroupByYear(List<Domain.Entities.Expense> expenses, DateTime now)
    {
        var last5Years = Enumerable.Range(0, 5)
            .Select(i => now.AddYears(-i).Year)
            .Reverse()
            .ToList();

        return last5Years.Select(year =>
        {
            var yearExpenses = expenses.Where(e => e.TransactionDate.Year == year).ToList();

            return new ExpenseSummaryDto(
                year.ToString(),
                yearExpenses.Sum(e => e.Amount),
                yearExpenses.Count,
                yearExpenses.GroupBy(e => e.Category.Name)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount))
            );
        }).ToList();
    }
}
