using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Application.Features.Expenses.Queries.GetExpenseGeneralInfo;

public record GetExpenseGeneralInfoQuery : IRequest<ExpenseGeneralInfoDto>
{
    public string? TargetCurrency { get; init; }
}

public class GetExpenseGeneralInfoQueryHandler : IRequestHandler<GetExpenseGeneralInfoQuery, ExpenseGeneralInfoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly ICurrencyExchangeService _currencyService;

    public GetExpenseGeneralInfoQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        ICurrencyExchangeService currencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _currencyService = currencyService;
    }

    public async Task<ExpenseGeneralInfoDto> Handle(GetExpenseGeneralInfoQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUserService.UserId;

        var userDetails = await _identityService.GetUserDetailsAsync(userId!);
        var prefCurrency = request.TargetCurrency ?? userDetails?.PreferredCurrency ?? "TRY";

        // Week: Monday to now
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1).Date;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var weeklyTotal = await GetConvertedTotalAsync(
            _context.Expenses.Where(e => e.CreatedBy == userId && e.TransactionDate >= startOfWeek),
            prefCurrency, cancellationToken);

        var monthlyTotal = await GetConvertedTotalAsync(
            _context.Expenses.Where(e => e.CreatedBy == userId && e.TransactionDate >= startOfMonth),
            prefCurrency, cancellationToken);

        var yearlyTotal = await GetConvertedTotalAsync(
            _context.Expenses.Where(e => e.CreatedBy == userId && e.TransactionDate >= startOfYear),
            prefCurrency, cancellationToken);

        var recurringThisMonth = await GetConvertedTotalAsync(
            _context.Expenses.Where(e => e.CreatedBy == userId && e.TransactionDate >= startOfMonth && e.IsRecurring),
            prefCurrency, cancellationToken);

        return new ExpenseGeneralInfoDto(
            weeklyTotal,
            monthlyTotal,
            yearlyTotal,
            recurringThisMonth);
    }

    private async Task<decimal> GetConvertedTotalAsync(
        IQueryable<Domain.Entities.Expense> query, 
        string targetCurrency, 
        CancellationToken cancellationToken)
    {
        var groups = await query
            .GroupBy(e => e.Currency ?? "TRY")
            .Select(g => new { Currency = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);

        decimal total = 0;
        foreach (var group in groups)
        {
            total += await _currencyService.ConvertAsync(group.Total, group.Currency, targetCurrency, cancellationToken);
        }
        return total;
    }
}
