using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Application.Features.Expenses.Queries.GetExpenseById;

public record GetExpenseByIdQuery(int Id) : IRequest<ExpenseDto>;

public class GetExpenseByIdQueryHandler : IRequestHandler<GetExpenseByIdQuery, ExpenseDto>
{
    private readonly IApplicationDbContext _context;

    public GetExpenseByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExpenseDto> Handle(GetExpenseByIdQuery request, CancellationToken cancellationToken)
    {
        var expense = await _context.Expenses
            .Include(e => e.Category)
            .Where(e => e.Id == request.Id)
            .Select(e => new ExpenseDto(
                e.Id,
                e.Description,
                e.Amount,
                e.PaymentMethod,
                e.IsRecurring,
                e.TransactionDate,
                e.CategoryId,
                e.Category.NameEn,
                e.Currency ?? "TRY"))
            .FirstOrDefaultAsync(cancellationToken);

        if (expense == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Expense), request.Id);
        }

        return expense;
    }
}
