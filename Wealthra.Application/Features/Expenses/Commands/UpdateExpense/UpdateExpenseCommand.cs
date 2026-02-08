using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Expenses.Commands.UpdateExpense;

public record UpdateExpenseCommand : IRequest<Unit>
{
    public int Id { get; init; }
    public string Description { get; init; }
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; }
    public bool IsRecurring { get; init; }
    public int CategoryId { get; init; }
    public DateTime TransactionDate { get; init; }
}

public class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseCommandValidator()
    {
        RuleFor(v => v.Id)
            .GreaterThan(0);

        RuleFor(v => v.Description)
            .MaximumLength(200).NotEmpty();

        RuleFor(v => v.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0.");

        RuleFor(v => v.CategoryId)
            .GreaterThan(0);
    }
}

public class UpdateExpenseCommandHandler : IRequestHandler<UpdateExpenseCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateExpenseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(UpdateExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (expense == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Expense), request.Id);
        }

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Category), request.CategoryId);
        }

        var oldAmount = expense.Amount;
        var oldCategoryId = expense.CategoryId;

        expense.Description = request.Description;
        expense.Amount = request.Amount;
        expense.PaymentMethod = request.PaymentMethod;
        expense.IsRecurring = request.IsRecurring;
        expense.CategoryId = request.CategoryId;
        expense.TransactionDate = request.TransactionDate;

        // Update budget logic: Adjust CurrentAmount
        if (oldAmount != request.Amount || oldCategoryId != request.CategoryId)
        {
            // Remove old amount from old budget
            if (oldCategoryId > 0)
            {
                var oldBudget = await _context.Budgets
                    .FirstOrDefaultAsync(b => b.CategoryId == oldCategoryId && b.CreatedBy == _currentUserService.UserId, cancellationToken);

                if (oldBudget != null)
                {
                    oldBudget.RemoveExpense(oldAmount);
                }
            }

            // Add new amount to new budget
            var newBudget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.CategoryId == request.CategoryId && b.CreatedBy == _currentUserService.UserId, cancellationToken);

            if (newBudget != null)
            {
                newBudget.AddExpense(request.Amount);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
