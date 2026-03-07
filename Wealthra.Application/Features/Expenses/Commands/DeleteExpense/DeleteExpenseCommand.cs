using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Commands.AnalyzeSpendingAnomalies;

namespace Wealthra.Application.Features.Expenses.Commands.DeleteExpense;

public record DeleteExpenseCommand(int Id) : IRequest<Unit>;

public class DeleteExpenseCommandHandler : IRequestHandler<DeleteExpenseCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISender _sender;

    public DeleteExpenseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, ISender sender)
    {
        _context = context;
        _currentUserService = currentUserService;
        _sender = sender;
    }

    public async Task<Unit> Handle(DeleteExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (expense == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Expense), request.Id);
        }

        // Update budget: Remove expense amount from budget
        var budget = await _context.Budgets
            .FirstOrDefaultAsync(b => b.CategoryId == expense.CategoryId && b.CreatedBy == _currentUserService.UserId, cancellationToken);

        if (budget != null)
        {
            budget.RemoveExpense(expense.Amount);
        }

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync(cancellationToken);

        // Trigger Anomaly Analysis for this month
        await _sender.Send(new AnalyzeSpendingAnomaliesCommand 
        { 
            Year = expense.TransactionDate.Year, 
            Month = expense.TransactionDate.Month 
        }, cancellationToken);

        return Unit.Value;
    }
}
