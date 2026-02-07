using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;
using Wealthra.Domain.Exceptions;

namespace Wealthra.Application.Features.Expenses.Commands.CreateExpense
{
    // 1. The Command (Input)
    public record CreateExpenseCommand : IRequest<int>
    {
        public string Description { get; init; }
        public decimal Amount { get; init; }
        public string PaymentMethod { get; init; }
        public bool IsRecurring { get; init; }
        public int CategoryId { get; init; }
        public DateTime TransactionDate { get; init; }
    }

    // 2. The Validator
    public class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
    {
        public CreateExpenseCommandValidator()
        {
            RuleFor(v => v.Description)
                .MaximumLength(200).NotEmpty();

            RuleFor(v => v.Amount)
                .GreaterThan(0).WithMessage("Amount must be greater than 0.");

            RuleFor(v => v.CategoryId)
                .GreaterThan(0);
        }
    }

    // 3. The Handler
    public class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public CreateExpenseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<int> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
        {
            // A. Check if Category exists
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

            if (category == null)
            {
                // In production, use a specific NotFoundException
                throw new Exception($"Category {request.CategoryId} not found.");
            }

            // B. Create Expense
            var entity = new Expense
            {
                Description = request.Description,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                IsRecurring = request.IsRecurring,
                CategoryId = request.CategoryId,
                TransactionDate = request.TransactionDate,
                // CreatedBy is handled by Infrastructure Auditing automatically
            };

            // C. Budget Logic (Domain Logic)
            // Find active budget for this category
            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.CategoryId == request.CategoryId && b.CreatedBy == _currentUserService.UserId, cancellationToken);

            if (budget != null)
            {
                // This method creates Domain Events if limit exceeded
                budget.AddExpense(request.Amount);
            }

            _context.Expenses.Add(entity);

            // D. Save (Infrastructure dispatches Domain Events here)
            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}