using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Commands.AnalyzeSpendingAnomalies;
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
        public string Currency { get; init; } = "TRY";
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
        private readonly ISender _sender;
        private readonly ICurrencyExchangeService _currencyService;

        public CreateExpenseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, ISender sender, ICurrencyExchangeService currencyService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _sender = sender;
            _currencyService = currencyService;
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
                Currency = request.Currency ?? "TRY",
                // CreatedBy is handled by Infrastructure Auditing automatically
            };

            // C. Budget Logic (Domain Logic)
            // Find active budget for this category
            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.CategoryId == request.CategoryId && b.CreatedBy == _currentUserService.UserId, cancellationToken);

            if (budget != null)
            {
                decimal amountToAdd = request.Amount;
                if (!string.Equals(request.Currency ?? "TRY", budget.Currency ?? "TRY", StringComparison.OrdinalIgnoreCase))
                {
                    amountToAdd = await _currencyService.ConvertAsync(request.Amount, request.Currency ?? "TRY", budget.Currency ?? "TRY", cancellationToken);
                }
                
                // This method creates Domain Events if limit exceeded
                budget.AddExpense(amountToAdd);
            }

            _context.Expenses.Add(entity);

            // D. Save (Infrastructure dispatches Domain Events here)
            await _context.SaveChangesAsync(cancellationToken);

            // E. Trigger Anomaly Analysis in the background for this month
            await _sender.Send(new AnalyzeSpendingAnomaliesCommand 
            { 
                Year = request.TransactionDate.Year, 
                Month = request.TransactionDate.Month 
            }, cancellationToken);

            return entity.Id;
        }
    }
}