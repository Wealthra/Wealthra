using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Budgets.Models;

namespace Wealthra.Application.Features.Budgets.Queries.GetBudgetById;

public record GetBudgetByIdQuery(int Id) : IRequest<BudgetDto>;

public class GetBudgetByIdQueryHandler : IRequestHandler<GetBudgetByIdQuery, BudgetDto>
{
    private readonly IApplicationDbContext _context;

    public GetBudgetByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BudgetDto> Handle(GetBudgetByIdQuery request, CancellationToken cancellationToken)
    {
        var budget = await _context.Budgets
            .Include(b => b.Category)
            .Where(b => b.Id == request.Id)
            .Select(b => new BudgetDto(
                b.Id,
                b.LimitAmount,
                b.CurrentAmount,
                b.LimitAmount > 0 ? (b.CurrentAmount / b.LimitAmount) * 100 : 0,
                GetBudgetStatus(b.CurrentAmount, b.LimitAmount),
                b.CategoryId,
                b.Category.Name))
            .FirstOrDefaultAsync(cancellationToken);

        if (budget == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Budget), request.Id);
        }

        return budget;
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
