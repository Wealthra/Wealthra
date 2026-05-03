using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Categories.Models;
using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Application.Features.Expenses.Queries.GetExpenseById;

public record GetExpenseByIdQuery(
    int Id,
    CategoryDisplayLanguage CategoryLanguage = CategoryDisplayLanguage.English,
    string? Currency = null) : IRequest<ExpenseDto>;

public class GetExpenseByIdQueryHandler : IRequestHandler<GetExpenseByIdQuery, ExpenseDto>
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrencyExchangeService _currencyService;
    private readonly IDisplayCurrencyService _displayCurrencyService;

    public GetExpenseByIdQueryHandler(
        IApplicationDbContext context,
        ICurrencyExchangeService currencyService,
        IDisplayCurrencyService displayCurrencyService)
    {
        _context = context;
        _currencyService = currencyService;
        _displayCurrencyService = displayCurrencyService;
    }

    public async Task<ExpenseDto> Handle(GetExpenseByIdQuery request, CancellationToken cancellationToken)
    {
        var useTr = request.CategoryLanguage == CategoryDisplayLanguage.Turkish;
        var e = await _context.Expenses
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (e == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Expense), request.Id);
        }

        var targetCurrency = await _displayCurrencyService.GetEffectiveCurrencyAsync(request.Currency, cancellationToken);
        var source = string.IsNullOrWhiteSpace(e.Currency) ? DefaultCurrency : e.Currency.ToUpperInvariant();
        var amount = e.Amount;
        if (!string.Equals(source, targetCurrency, StringComparison.Ordinal))
        {
            amount = await _currencyService.ConvertAsync(e.Amount, source, targetCurrency, cancellationToken);
        }

        return new ExpenseDto(
            e.Id,
            e.Description,
            amount,
            e.PaymentMethod,
            e.IsRecurring,
            e.TransactionDate,
            e.CategoryId,
            useTr ? e.Category.NameTr : e.Category.NameEn,
            targetCurrency);
    }
}
