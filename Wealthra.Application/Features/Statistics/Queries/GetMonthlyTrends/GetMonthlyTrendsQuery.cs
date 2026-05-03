using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Statistics.Models;

namespace Wealthra.Application.Features.Statistics.Queries.GetMonthlyTrends;

public record GetMonthlyTrendsQuery : IRequest<MonthlyTrendsDto>
{
    public int? Year { get; init; }
    [FromQuery(Name = "currency")]
    public string? TargetCurrency { get; init; }
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
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrencyExchangeService _currencyService;
    private readonly IDisplayCurrencyService _displayCurrencyService;

    public GetMonthlyTrendsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICurrencyExchangeService currencyService,
        IDisplayCurrencyService displayCurrencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _currencyService = currencyService;
        _displayCurrencyService = displayCurrencyService;
    }

    public async Task<MonthlyTrendsDto> Handle(GetMonthlyTrendsQuery request, CancellationToken cancellationToken)
    {
        var year = request.Year ?? DateTime.UtcNow.Year;
        var effective = await _displayCurrencyService.GetEffectiveCurrencyAsync(request.TargetCurrency, cancellationToken);

        var expenses = await _context.Expenses
            .Where(e => e.CreatedBy == _currentUserService.UserId &&
                       e.TransactionDate.Year == year)
            .ToListAsync(cancellationToken);

        var incomes = await _context.Incomes
            .Where(i => i.CreatedBy == _currentUserService.UserId &&
                       i.TransactionDate.Year == year)
            .ToListAsync(cancellationToken);

        var monthlyData = new List<MonthlyTrendItem>();

        for (int month = 1; month <= 12; month++)
        {
            decimal monthExpenses = 0;
            foreach (var e in expenses.Where(x => x.TransactionDate.Month == month))
            {
                var source = string.IsNullOrWhiteSpace(e.Currency) ? DefaultCurrency : e.Currency.ToUpperInvariant();
                var amt = e.Amount;
                if (!string.Equals(source, effective, StringComparison.Ordinal))
                {
                    amt = await _currencyService.ConvertAsync(e.Amount, source, effective, cancellationToken);
                }

                monthExpenses += amt;
            }

            decimal monthIncomes = 0;
            foreach (var i in incomes.Where(x => x.TransactionDate.Month == month))
            {
                var source = string.IsNullOrWhiteSpace(i.Currency) ? DefaultCurrency : i.Currency.ToUpperInvariant();
                var amt = i.Amount;
                if (!string.Equals(source, effective, StringComparison.Ordinal))
                {
                    amt = await _currencyService.ConvertAsync(i.Amount, source, effective, cancellationToken);
                }

                monthIncomes += amt;
            }

            var netAmount = monthIncomes - monthExpenses;
            var monthName = new DateTime(year, month, 1).ToString("MMMM", CultureInfo.InvariantCulture);

            monthlyData.Add(new MonthlyTrendItem(
                month,
                monthName,
                monthIncomes,
                monthExpenses,
                netAmount));
        }

        return new MonthlyTrendsDto(year, monthlyData, effective);
    }
}
