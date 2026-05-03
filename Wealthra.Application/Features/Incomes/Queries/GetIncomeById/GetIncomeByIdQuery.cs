using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Incomes.Models;

namespace Wealthra.Application.Features.Incomes.Queries.GetIncomeById;

public record GetIncomeByIdQuery(int Id, string? Currency = null) : IRequest<IncomeDto>;

public class GetIncomeByIdQueryHandler : IRequestHandler<GetIncomeByIdQuery, IncomeDto>
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrencyExchangeService _currencyService;
    private readonly IDisplayCurrencyService _displayCurrencyService;

    public GetIncomeByIdQueryHandler(
        IApplicationDbContext context,
        ICurrencyExchangeService currencyService,
        IDisplayCurrencyService displayCurrencyService)
    {
        _context = context;
        _currencyService = currencyService;
        _displayCurrencyService = displayCurrencyService;
    }

    public async Task<IncomeDto> Handle(GetIncomeByIdQuery request, CancellationToken cancellationToken)
    {
        var i = await _context.Incomes.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (i == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Income), request.Id);
        }

        var targetCurrency = await _displayCurrencyService.GetEffectiveCurrencyAsync(request.Currency, cancellationToken);
        var source = string.IsNullOrWhiteSpace(i.Currency) ? DefaultCurrency : i.Currency.ToUpperInvariant();
        var amount = i.Amount;
        if (!string.Equals(source, targetCurrency, StringComparison.Ordinal))
        {
            amount = await _currencyService.ConvertAsync(i.Amount, source, targetCurrency, cancellationToken);
        }

        return new IncomeDto(
            i.Id,
            i.Name,
            amount,
            i.Method,
            i.IsRecurring,
            i.TransactionDate,
            targetCurrency);
    }
}
