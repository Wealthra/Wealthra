using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Statistics.Models;

namespace Wealthra.Application.Features.Statistics.Queries.GetSpendingBreakdown;

public record GetSpendingBreakdownQuery : IRequest<SpendingBreakdownDto>
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    [FromQuery(Name = "currency")]
    public string? TargetCurrency { get; init; }
}

public class GetSpendingBreakdownQueryValidator : AbstractValidator<GetSpendingBreakdownQuery>
{
    public GetSpendingBreakdownQueryValidator()
    {
        RuleFor(v => v)
            .Must(v => !v.StartDate.HasValue || !v.EndDate.HasValue || v.StartDate.Value <= v.EndDate.Value)
            .WithMessage("StartDate must be before or equal to EndDate.");
    }
}

public class GetSpendingBreakdownQueryHandler : IRequestHandler<GetSpendingBreakdownQuery, SpendingBreakdownDto>
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrencyExchangeService _currencyService;
    private readonly IDisplayCurrencyService _displayCurrencyService;

    public GetSpendingBreakdownQueryHandler(
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

    public async Task<SpendingBreakdownDto> Handle(GetSpendingBreakdownQuery request, CancellationToken cancellationToken)
    {
        var startDate = request.StartDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var endDate = request.EndDate ?? DateTime.UtcNow;
        var effective = await _displayCurrencyService.GetEffectiveCurrencyAsync(request.TargetCurrency, cancellationToken);

        var expenses = await _context.Expenses
            .Include(e => e.Category)
            .Where(e => e.CreatedBy == _currentUserService.UserId &&
                       e.TransactionDate >= startDate &&
                       e.TransactionDate <= endDate)
            .ToListAsync(cancellationToken);

        var convertedByCategory = new Dictionary<int, (string Name, decimal Amount, int Count)>();
        foreach (var e in expenses)
        {
            var source = string.IsNullOrWhiteSpace(e.Currency) ? DefaultCurrency : e.Currency.ToUpperInvariant();
            var amt = e.Amount;
            if (!string.Equals(source, effective, StringComparison.Ordinal))
            {
                amt = await _currencyService.ConvertAsync(e.Amount, source, effective, cancellationToken);
            }

            if (!convertedByCategory.TryGetValue(e.CategoryId, out var cur))
            {
                cur = (e.Category.NameEn, 0, 0);
            }

            convertedByCategory[e.CategoryId] = (cur.Name, cur.Amount + amt, cur.Count + 1);
        }

        var totalAmount = convertedByCategory.Values.Sum(v => v.Amount);

        var categoryBreakdown = convertedByCategory
            .Select(kv => new CategoryBreakdownItem(
                kv.Value.Name,
                kv.Value.Amount,
                totalAmount > 0 ? (kv.Value.Amount / totalAmount) * 100 : 0,
                kv.Value.Count))
            .OrderByDescending(c => c.Amount)
            .ToList();

        return new SpendingBreakdownDto(
            categoryBreakdown,
            totalAmount,
            startDate,
            endDate,
            effective);
    }
}
