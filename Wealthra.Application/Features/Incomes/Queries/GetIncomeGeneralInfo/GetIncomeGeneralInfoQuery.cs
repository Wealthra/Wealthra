using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Incomes.Models;

namespace Wealthra.Application.Features.Incomes.Queries.GetIncomeGeneralInfo;

public record GetIncomeGeneralInfoQuery : IRequest<IncomeGeneralInfoDto>;

public class GetIncomeGeneralInfoQueryHandler : IRequestHandler<GetIncomeGeneralInfoQuery, IncomeGeneralInfoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetIncomeGeneralInfoQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IncomeGeneralInfoDto> Handle(GetIncomeGeneralInfoQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUserService.UserId;

        // Week: Monday to now
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1).Date;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var twelveMonthsAgo = startOfMonth.AddMonths(-11); // 12 months including current

        var weeklyTotal = await _context.Incomes
            .Where(i => i.CreatedBy == userId && i.TransactionDate >= startOfWeek)
            .SumAsync(i => i.Amount, cancellationToken);

        var monthlyTotal = await _context.Incomes
            .Where(i => i.CreatedBy == userId && i.TransactionDate >= startOfMonth)
            .SumAsync(i => i.Amount, cancellationToken);

        var yearlyTotal = await _context.Incomes
            .Where(i => i.CreatedBy == userId && i.TransactionDate >= startOfYear)
            .SumAsync(i => i.Amount, cancellationToken);

        // 12-month rolling average: total income over last 12 months / 12
        var last12MonthsTotal = await _context.Incomes
            .Where(i => i.CreatedBy == userId && i.TransactionDate >= twelveMonthsAgo)
            .SumAsync(i => i.Amount, cancellationToken);

        var averageMonthlyIncome = Math.Round(last12MonthsTotal / 12, 2);

        return new IncomeGeneralInfoDto(
            weeklyTotal,
            monthlyTotal,
            yearlyTotal,
            averageMonthlyIncome);
    }
}
