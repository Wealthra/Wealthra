using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.ListManualExchangeRates;

public record ListManualExchangeRatesQuery : IRequest<List<ManualExchangeRateDto>>;

public class ListManualExchangeRatesQueryHandler : IRequestHandler<ListManualExchangeRatesQuery, List<ManualExchangeRateDto>>
{
    private readonly IApplicationDbContext _db;

    public ListManualExchangeRatesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ManualExchangeRateDto>> Handle(ListManualExchangeRatesQuery request, CancellationToken cancellationToken)
    {
        return await _db.ManualExchangeRates.AsNoTracking()
            .OrderBy(x => x.FromCurrency)
            .ThenBy(x => x.ToCurrency)
            .Select(x => new ManualExchangeRateDto(x.Id, x.FromCurrency, x.ToCurrency, x.Rate, x.UpdatedOn))
            .ToListAsync(cancellationToken);
    }
}
