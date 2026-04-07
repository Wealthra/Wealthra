using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using Wealthra.Application.Features.Expenses.Commands.CreateExpense;

namespace Wealthra.Application.Features.Expenses.Commands.CreateExpensesBulk;

public class CreateExpenseBulkItem
{
    public string Description { get; init; }
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; }
    public bool IsRecurring { get; init; }
    public int CategoryId { get; init; }
    public DateTime TransactionDate { get; init; }
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
    private readonly ISender _sender;

    public CreateExpensesBulkCommandHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<IReadOnlyList<int>> Handle(CreateExpensesBulkCommand request, CancellationToken cancellationToken)
    {
        var ids = new List<int>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var id = await _sender.Send(
                new CreateExpenseCommand
                {
                    Description = item.Description,
                    Amount = item.Amount,
                    PaymentMethod = item.PaymentMethod ?? string.Empty,
                    IsRecurring = item.IsRecurring,
                    CategoryId = item.CategoryId,
                    TransactionDate = item.TransactionDate
                },
                cancellationToken);
            ids.Add(id);
        }

        return ids;
    }
}
