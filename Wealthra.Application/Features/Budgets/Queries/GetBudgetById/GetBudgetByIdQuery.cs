using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Budgets.Models;
using Wealthra.Application.Features.Categories.Models;

namespace Wealthra.Application.Features.Budgets.Queries.GetBudgetById;

public record GetBudgetByIdQuery(
    int Id,
    CategoryDisplayLanguage CategoryLanguage = CategoryDisplayLanguage.English,
    string? Currency = null) : IRequest<BudgetDto>;

public class GetBudgetByIdQueryHandler : IRequestHandler<GetBudgetByIdQuery, BudgetDto>
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrencyExchangeService _currencyService;
    private readonly IDisplayCurrencyService _displayCurrencyService;

    public GetBudgetByIdQueryHandler(
        IApplicationDbContext context,
        ICurrencyExchangeService currencyService,
        IDisplayCurrencyService displayCurrencyService)
    {
        _context = context;
        _currencyService = currencyService;
        _displayCurrencyService = displayCurrencyService;
    }

    public async Task<BudgetDto> Handle(GetBudgetByIdQuery request, CancellationToken cancellationToken)
    {
        var useTr = request.CategoryLanguage == CategoryDisplayLanguage.Turkish;
        var b = await _context.Budgets
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (b == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Budget), request.Id);
        }

        var targetCurrency = await _displayCurrencyService.GetEffectiveCurrencyAsync(request.Currency, cancellationToken);
        var source = string.IsNullOrWhiteSpace(b.Currency) ? DefaultCurrency : b.Currency.ToUpperInvariant();
        var limit = b.LimitAmount;
        var current = b.CurrentAmount;
        if (!string.Equals(source, targetCurrency, StringComparison.Ordinal))
        {
            limit = await _currencyService.ConvertAsync(b.LimitAmount, source, targetCurrency, cancellationToken);
            current = await _currencyService.ConvertAsync(b.CurrentAmount, source, targetCurrency, cancellationToken);
        }

        return new BudgetDto(
            b.Id,
            limit,
            current,
            limit > 0 ? (current / limit) * 100 : 0,
            GetBudgetStatus(current, limit),
            b.CategoryId,
            useTr ? b.Category.NameTr : b.Category.NameEn,
            targetCurrency);
    }

    private static string GetBudgetStatus(decimal currentAmount, decimal limitAmount)
    {
        if (limitAmount == 0) return "Safe";

        var percentage = (currentAmount / limitAmount) * 100;

        return percentage switch
        {
            >= 100 => "Exceeded",
            >= 80 => "Warning",
            _ => "Safe"
        };
    }
}
