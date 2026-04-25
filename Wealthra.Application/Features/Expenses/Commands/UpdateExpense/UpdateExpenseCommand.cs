using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Commands.AnalyzeSpendingAnomalies;

namespace Wealthra.Application.Features.Expenses.Commands.UpdateExpense;

public record UpdateExpenseCommand : IRequest<Unit>
{
    public int Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public bool IsRecurring { get; init; }
    public int CategoryId { get; init; }
    public DateTime TransactionDate { get; init; }
    public string Currency { get; init; } = "TRY";
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
    private readonly ISender _sender;
    private readonly ICurrencyExchangeService _currencyService;

    public UpdateExpenseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, ISender sender, ICurrencyExchangeService currencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _sender = sender;
        _currencyService = currencyService;
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
        var oldCurrency = expense.Currency ?? "TRY";
        var oldCategoryId = expense.CategoryId;

        expense.Description = request.Description;
        expense.Amount = request.Amount;
        expense.Currency = request.Currency ?? "TRY";
        expense.PaymentMethod = request.PaymentMethod;
        expense.IsRecurring = request.IsRecurring;
        expense.CategoryId = request.CategoryId;
        expense.TransactionDate = request.TransactionDate;

        // Update budget logic: Adjust CurrentAmount
        if (oldAmount != request.Amount || oldCategoryId != request.CategoryId || !string.Equals(oldCurrency, request.Currency ?? "TRY", StringComparison.OrdinalIgnoreCase))
        {
            // Remove old amount from old budget
            if (oldCategoryId > 0)
            {
                var oldBudget = await _context.Budgets
                    .FirstOrDefaultAsync(b => b.CategoryId == oldCategoryId && b.CreatedBy == _currentUserService.UserId, cancellationToken);

                if (oldBudget != null)
                {
                    decimal removeAmount = oldAmount;
                    if (!string.Equals(oldCurrency, oldBudget.Currency ?? "TRY", StringComparison.OrdinalIgnoreCase))
                        removeAmount = await _currencyService.ConvertAsync(oldAmount, oldCurrency, oldBudget.Currency ?? "TRY", cancellationToken);
                    oldBudget.RemoveExpense(removeAmount);
                }
            }

            // Add new amount to new budget
            var newBudget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.CategoryId == request.CategoryId && b.CreatedBy == _currentUserService.UserId, cancellationToken);

            if (newBudget != null)
            {
                decimal addAmount = request.Amount;
                if (!string.Equals(request.Currency ?? "TRY", newBudget.Currency ?? "TRY", StringComparison.OrdinalIgnoreCase))
                    addAmount = await _currencyService.ConvertAsync(request.Amount, request.Currency ?? "TRY", newBudget.Currency ?? "TRY", cancellationToken);
                newBudget.AddExpense(addAmount);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Trigger Anomaly Analysis for this month
        await _sender.Send(new AnalyzeSpendingAnomaliesCommand 
        { 
            Year = request.TransactionDate.Year, 
            Month = request.TransactionDate.Month 
        }, cancellationToken);

        return Unit.Value;
    }
}
