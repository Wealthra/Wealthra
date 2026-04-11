using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Incomes.Models;

namespace Wealthra.Application.Features.Incomes.Queries.GetIncomeById;

public record GetIncomeByIdQuery(int Id) : IRequest<IncomeDto>;

public class GetIncomeByIdQueryHandler : IRequestHandler<GetIncomeByIdQuery, IncomeDto>
{
    private readonly IApplicationDbContext _context;

    public GetIncomeByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IncomeDto> Handle(GetIncomeByIdQuery request, CancellationToken cancellationToken)
    {
        var income = await _context.Incomes
            .Where(i => i.Id == request.Id)
            .Select(i => new IncomeDto(
                i.Id,
                i.Name,
                i.Amount,
                i.Method,
                i.IsRecurring,
                i.TransactionDate,
                i.Currency ?? "TRY"))
            .FirstOrDefaultAsync(cancellationToken);

        if (income == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Income), request.Id);
        }

        return income;
    }
}
