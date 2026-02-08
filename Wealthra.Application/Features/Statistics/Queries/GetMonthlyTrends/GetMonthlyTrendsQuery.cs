using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Statistics.Models;

namespace Wealthra.Application.Features.Statistics.Queries.GetMonthlyTrends;

public record GetMonthlyTrendsQuery : IRequest<MonthlyTrendsDto>
{
    public int? Year { get; init; }
}

public class GetMonthlyTrendsQueryValidator : AbstractValidator<GetMonthlyTrendsQuery>
{
    public GetMonthlyTrendsQueryValidator()
    {
        RuleFor(v => v.Year)
            .GreaterThan(2000)
            .LessThanOrEqualTo(DateTime.UtcNow.Year + 1)
            .When(v => v.Year.HasValue)
            .WithMessage("Year must be between 2000 and next year.");
    }
}

public class GetMonthlyTrendsQueryHandler : IRequestHandler<GetMonthlyTrendsQuery, MonthlyTrendsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMonthlyTrendsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<MonthlyTrendsDto> Handle(GetMonthlyTrendsQuery request, CancellationToken cancellationToken)
    {
        var year = request.Year ?? DateTime.UtcNow.Year;

        // Get all expenses for the year
        var expenses = await _context.Expenses
            .Where(e => e.CreatedBy == _currentUserService.UserId &&
                       e.TransactionDate.Year == year)
            .ToListAsync(cancellationToken);

        // Get all incomes for the year
        var incomes = await _context.Incomes
            .Where(i => i.CreatedBy == _currentUserService.UserId &&
                       i.TransactionDate.Year == year)
            .ToListAsync(cancellationToken);

        // Group by month
        var monthlyData = new List<MonthlyTrendItem>();

        for (int month = 1; month <= 12; month++)
        {
            var monthExpenses = expenses
                .Where(e => e.TransactionDate.Month == month)
                .Sum(e => e.Amount);

            var monthIncomes = incomes
                .Where(i => i.TransactionDate.Month == month)
                .Sum(i => i.Amount);

            var netAmount = monthIncomes - monthExpenses;

            var monthName = new DateTime(year, month, 1).ToString("MMMM", CultureInfo.InvariantCulture);

            monthlyData.Add(new MonthlyTrendItem(
                month,
                monthName,
                monthIncomes,
                monthExpenses,
                netAmount));
        }

        return new MonthlyTrendsDto(year, monthlyData);
    }
}
