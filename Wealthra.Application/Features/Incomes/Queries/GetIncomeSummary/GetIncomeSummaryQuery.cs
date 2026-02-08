using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Incomes.Models;

namespace Wealthra.Application.Features.Incomes.Queries.GetIncomeSummary;

public record GetIncomeSummaryQuery : IRequest<List<IncomeSummaryDto>>
{
    public string Period { get; init; } = "Monthly";
}

public class GetIncomeSummaryQueryValidator : AbstractValidator<GetIncomeSummaryQuery>
{
    public GetIncomeSummaryQueryValidator()
    {
        RuleFor(v => v.Period)
            .Must(p => p == "Weekly" || p == "Monthly" || p == "Yearly")
            .WithMessage("Period must be Weekly, Monthly, or Yearly.");
    }
}

public class GetIncomeSummaryQueryHandler : IRequestHandler<GetIncomeSummaryQuery, List<IncomeSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetIncomeSummaryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<IncomeSummaryDto>> Handle(GetIncomeSummaryQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var incomes = await _context.Incomes
            .Where(i => i.CreatedBy == _currentUserService.UserId)
            .ToListAsync(cancellationToken);

        var groupedIncomes = request.Period switch
        {
            "Weekly" => GroupByWeek(incomes, now),
            "Monthly" => GroupByMonth(incomes, now),
            "Yearly" => GroupByYear(incomes, now),
            _ => GroupByMonth(incomes, now)
        };

        return groupedIncomes;
    }

    private List<IncomeSummaryDto> GroupByWeek(List<Domain.Entities.Income> incomes, DateTime now)
    {
        var last12Weeks = Enumerable.Range(0, 12)
            .Select(i => now.AddDays(-i * 7).Date)
            .Reverse()
            .ToList();

        return last12Weeks.Select(weekStart =>
        {
            var weekEnd = weekStart.AddDays(7);
            var weekIncomes = incomes.Where(i => i.TransactionDate >= weekStart && i.TransactionDate < weekEnd).ToList();

            return new IncomeSummaryDto(
                $"Week of {weekStart:MMM dd}",
                weekIncomes.Sum(i => i.Amount),
                weekIncomes.Count
            );
        }).ToList();
    }

    private List<IncomeSummaryDto> GroupByMonth(List<Domain.Entities.Income> incomes, DateTime now)
    {
        var last12Months = Enumerable.Range(0, 12)
            .Select(i => now.AddMonths(-i).Date)
            .Reverse()
            .ToList();

        return last12Months.Select(monthStart =>
        {
            var monthIncomes = incomes.Where(i =>
                i.TransactionDate.Year == monthStart.Year &&
                i.TransactionDate.Month == monthStart.Month).ToList();

            return new IncomeSummaryDto(
                monthStart.ToString("MMMM yyyy"),
                monthIncomes.Sum(i => i.Amount),
                monthIncomes.Count
            );
        }).ToList();
    }

    private List<IncomeSummaryDto> GroupByYear(List<Domain.Entities.Income> incomes, DateTime now)
    {
        var last5Years = Enumerable.Range(0, 5)
            .Select(i => now.AddYears(-i).Year)
            .Reverse()
            .ToList();

        return last5Years.Select(year =>
        {
            var yearIncomes = incomes.Where(i => i.TransactionDate.Year == year).ToList();

            return new IncomeSummaryDto(
                year.ToString(),
                yearIncomes.Sum(i => i.Amount),
                yearIncomes.Count
            );
        }).ToList();
    }
}
