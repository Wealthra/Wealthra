using MediatR;
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
}

public class GetIncomesQueryHandler : IRequestHandler<GetIncomesQuery, PaginatedList<IncomeDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetIncomesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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

        var incomesQuery = orderedQuery.Select(i => new IncomeDto(
            i.Id,
            i.Name,
            i.Amount,
            i.Method,
            i.IsRecurring,
            i.TransactionDate));

        return await PaginatedList<IncomeDto>.CreateAsync(
            incomesQuery,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
