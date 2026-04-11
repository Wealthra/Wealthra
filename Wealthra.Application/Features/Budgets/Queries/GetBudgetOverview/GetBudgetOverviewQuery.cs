using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Budgets.Models;

namespace Wealthra.Application.Features.Budgets.Queries.GetBudgetOverview;

public record GetBudgetOverviewQuery : IRequest<BudgetOverviewDto>
{
    public string? TargetCurrency { get; init; }
}

public class GetBudgetOverviewQueryHandler : IRequestHandler<GetBudgetOverviewQuery, BudgetOverviewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly ICurrencyExchangeService _currencyService;

    public GetBudgetOverviewQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService, IIdentityService identityService, ICurrencyExchangeService currencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _currencyService = currencyService;
    }

    public async Task<BudgetOverviewDto> Handle(GetBudgetOverviewQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1);
        var currentMonthEnd = currentMonthStart.AddMonths(1);

        var userDetails = await _identityService.GetUserDetailsAsync(_currentUserService.UserId);
        var prefCurrency = request.TargetCurrency ?? userDetails?.PreferredCurrency ?? "TRY";

        var budgets = await _context.Budgets
            .Where(b => b.CreatedBy == _currentUserService.UserId)
            .Select(b => new { b.LimitAmount, b.CurrentAmount, Currency = b.Currency ?? "TRY" })
            .ToListAsync(cancellationToken);

        decimal totalLimit = 0;
        decimal totalSpent = 0;
        int budgetsExceeded = 0;
        int budgetsWarning = 0;

        foreach (var b in budgets)
        {
            var limitInPref = await _currencyService.ConvertAsync(b.LimitAmount, b.Currency, prefCurrency, cancellationToken);
            var spentInPref = await _currencyService.ConvertAsync(b.CurrentAmount, b.Currency, prefCurrency, cancellationToken);

            totalLimit += limitInPref;
            totalSpent += spentInPref;

            if (b.LimitAmount > 0)
            {
                var percentage = (b.CurrentAmount / b.LimitAmount) * 100;
                if (percentage >= 100) budgetsExceeded++;
                else if (percentage >= 80) budgetsWarning++;
            }
        }

        var percentageUsed = totalLimit > 0 ? (totalSpent / totalLimit) * 100 : 0;

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
            budgetsWarning,
            prefCurrency);
    }
}
