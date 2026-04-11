using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Commands.AnalyzeSpendingAnomalies;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Expenses.Commands.CreateExpensesBulk;

public class CreateExpenseBulkItem
{
    public string Description { get; init; }
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; }
    public bool IsRecurring { get; init; }
    public int CategoryId { get; init; }
    public DateTime TransactionDate { get; init; }
    public string Currency { get; init; } = "TRY";
}


public record CreateExpensesBulkCommand : IRequest<IReadOnlyList<int>>
{
    public IReadOnlyList<CreateExpenseBulkItem> Items { get; init; }
}

public class CreateExpensesBulkCommandValidator : AbstractValidator<CreateExpensesBulkCommand>
{
    public CreateExpensesBulkCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one expense is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description)
                .MaximumLength(200)
                .NotEmpty();

            item.RuleFor(i => i.Amount)
                .GreaterThan(0)
                .WithMessage("Amount must be greater than 0.");

            item.RuleFor(i => i.CategoryId)
                .GreaterThan(0);
        });
    }
}

public class CreateExpensesBulkCommandHandler : IRequestHandler<CreateExpensesBulkCommand, IReadOnlyList<int>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISender _sender;
    private readonly ICurrencyExchangeService _currencyService;

    public CreateExpensesBulkCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISender sender,
        ICurrencyExchangeService currencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _sender = sender;
        _currencyService = currencyService;
    }

    public async Task<IReadOnlyList<int>> Handle(CreateExpensesBulkCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var categoryIds = request.Items.Select(i => i.CategoryId).Distinct().ToList();

        var existingCategoryIds = await _context.Categories
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var missing = categoryIds.Except(existingCategoryIds).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Categories not found: {string.Join(", ", missing)}.");
        }

        var budgets = await _context.Budgets
            .Where(b => b.CreatedBy == userId && categoryIds.Contains(b.CategoryId))
            .ToDictionaryAsync(b => b.CategoryId, cancellationToken);

        var entities = new List<Expense>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var entity = new Expense
            {
                Description = item.Description,
                Amount = item.Amount,
                PaymentMethod = item.PaymentMethod ?? string.Empty,
                IsRecurring = item.IsRecurring,
                CategoryId = item.CategoryId,
                TransactionDate = item.TransactionDate,
                Currency = item.Currency ?? "TRY"
            };
            entities.Add(entity);

            if (budgets.TryGetValue(item.CategoryId, out var budget))
            {
                decimal amountToAdd = item.Amount;
                var itemCurr = item.Currency ?? "TRY";
                var budgetCurr = budget.Currency ?? "TRY";
                
                if (!string.Equals(itemCurr, budgetCurr, StringComparison.OrdinalIgnoreCase))
                {
                    amountToAdd = await _currencyService.ConvertAsync(item.Amount, itemCurr, budgetCurr, cancellationToken);
                }
                budget.AddExpense(amountToAdd);
            }
        }

        _context.Expenses.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);

        var distinctMonths = request.Items
            .Select(i => (Year: i.TransactionDate.Year, Month: i.TransactionDate.Month))
            .Distinct()
            .ToList();

        foreach (var ym in distinctMonths)
        {
            await _sender.Send(
                new AnalyzeSpendingAnomaliesCommand { Year = ym.Year, Month = ym.Month },
                cancellationToken);
        }

        return entities.Select(e => e.Id).ToList();
    }
}
