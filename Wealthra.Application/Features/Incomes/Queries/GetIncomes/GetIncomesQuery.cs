using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Incomes.Models;

namespace Wealthra.Application.Features.Incomes.Queries.GetIncomes;

public record GetIncomesQuery : IRequest<PaginatedList<IncomeDto>>
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    /// <summary>When set, amounts are converted to this currency and <see cref="IncomeDto.Currency"/> reflects it.</summary>
    [FromQuery(Name = "currency")]
    public string? TargetCurrency { get; init; }
}

public class GetIncomesQueryHandler : IRequestHandler<GetIncomesQuery, PaginatedList<IncomeDto>>
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrencyExchangeService _currencyService;

    public GetIncomesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICurrencyExchangeService currencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _currencyService = currencyService;
    }

    public async Task<PaginatedList<IncomeDto>> Handle(GetIncomesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Incomes
            .Where(i => i.CreatedBy == _currentUserService.UserId)
            .AsQueryable();

        if (request.StartDate.HasValue)
        {
            query = query.Where(i => i.TransactionDate >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(i => i.TransactionDate <= request.EndDate.Value);
        }

        var orderedQuery = query.OrderByDescending(i => i.TransactionDate);

        var targetCurrency = request.TargetCurrency?.Trim();
        if (string.IsNullOrEmpty(targetCurrency))
        {
            var incomesQuery = orderedQuery.Select(i => new IncomeDto(
                i.Id,
                i.Name,
                i.Amount,
                i.Method,
                i.IsRecurring,
                i.TransactionDate,
                i.Currency ?? DefaultCurrency));

            return await PaginatedList<IncomeDto>.CreateAsync(
                incomesQuery,
                request.PageNumber,
                request.PageSize,
                cancellationToken);
        }

        targetCurrency = targetCurrency.ToUpperInvariant();

        var count = await orderedQuery.CountAsync(cancellationToken);
        var page = await orderedQuery
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = new List<IncomeDto>(page.Count);
        foreach (var i in page)
        {
            var source = string.IsNullOrWhiteSpace(i.Currency) ? DefaultCurrency : i.Currency.ToUpperInvariant();
            var amount = i.Amount;
            if (!string.Equals(source, targetCurrency, StringComparison.Ordinal))
            {
                amount = await _currencyService.ConvertAsync(i.Amount, source, targetCurrency, cancellationToken);
            }

            items.Add(new IncomeDto(
                i.Id,
                i.Name,
                amount,
                i.Method,
                i.IsRecurring,
                i.TransactionDate,
                targetCurrency));
        }

        return new PaginatedList<IncomeDto>(items, count, request.PageNumber, request.PageSize);
    }
}
