using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Incomes.Commands.UpdateIncome;

public record UpdateIncomeCommand : IRequest<Unit>
{
    public int Id { get; init; }
    public string Name { get; init; }
    public decimal Amount { get; init; }
    public string Method { get; init; }
    public bool IsRecurring { get; init; }
    public DateTime TransactionDate { get; init; }
    public string Currency { get; init; } = "TRY";
}

public class UpdateIncomeCommandValidator : AbstractValidator<UpdateIncomeCommand>
{
    public UpdateIncomeCommandValidator()
    {
        RuleFor(v => v.Id)
            .GreaterThan(0);

        RuleFor(v => v.Name)
            .MaximumLength(200)
            .NotEmpty();

        RuleFor(v => v.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0.");

        RuleFor(v => v.Method)
            .MaximumLength(100)
            .NotEmpty();
    }
}

public class UpdateIncomeCommandHandler : IRequestHandler<UpdateIncomeCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateIncomeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateIncomeCommand request, CancellationToken cancellationToken)
    {
        var income = await _context.Incomes
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (income == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Income), request.Id);
        }

        income.Name = request.Name;
        income.Amount = request.Amount;
        income.Method = request.Method;
        income.IsRecurring = request.IsRecurring;
        income.TransactionDate = request.TransactionDate;
        income.Currency = request.Currency ?? "TRY";

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
