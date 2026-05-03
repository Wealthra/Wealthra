using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Goals.Models;

namespace Wealthra.Application.Features.Goals.Queries.GetGoalById;

public record GetGoalByIdQuery(int Id, string? Currency = null) : IRequest<GoalDto>;

public class GetGoalByIdQueryHandler : IRequestHandler<GetGoalByIdQuery, GoalDto>
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrencyExchangeService _currencyService;
    private readonly IDisplayCurrencyService _displayCurrencyService;

    public GetGoalByIdQueryHandler(
        IApplicationDbContext context,
        ICurrencyExchangeService currencyService,
        IDisplayCurrencyService displayCurrencyService)
    {
        _context = context;
        _currencyService = currencyService;
        _displayCurrencyService = displayCurrencyService;
    }

    public async Task<GoalDto> Handle(GetGoalByIdQuery request, CancellationToken cancellationToken)
    {
        var g = await _context.Goals.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (g == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Goal), request.Id);
        }

        var targetCurrency = await _displayCurrencyService.GetEffectiveCurrencyAsync(request.Currency, cancellationToken);
        var source = string.IsNullOrWhiteSpace(g.Currency) ? DefaultCurrency : g.Currency.ToUpperInvariant();
        var targetAmt = g.TargetAmount;
        var currentAmt = g.CurrentAmount;
        if (!string.Equals(source, targetCurrency, StringComparison.Ordinal))
        {
            targetAmt = await _currencyService.ConvertAsync(g.TargetAmount, source, targetCurrency, cancellationToken);
            currentAmt = await _currencyService.ConvertAsync(g.CurrentAmount, source, targetCurrency, cancellationToken);
        }

        return new GoalDto(
            g.Id,
            g.Name,
            targetAmt,
            currentAmt,
            targetAmt > 0 ? (currentAmt / targetAmt) * 100 : 0,
            g.Deadline,
            g.CurrentAmount >= g.TargetAmount,
            targetCurrency);
    }
}
