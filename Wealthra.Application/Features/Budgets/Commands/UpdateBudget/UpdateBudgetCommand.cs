using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Budgets.Commands.UpdateBudget;

public record UpdateBudgetCommand : IRequest<Unit>
{
    public int Id { get; init; }
    public decimal LimitAmount { get; init; }
}

public class UpdateBudgetCommandValidator : AbstractValidator<UpdateBudgetCommand>
{
    public UpdateBudgetCommandValidator()
    {
        RuleFor(v => v.Id)
            .GreaterThan(0);

        RuleFor(v => v.LimitAmount)
            .GreaterThan(0)
            .WithMessage("Limit amount must be greater than 0.");
    }
}

public class UpdateBudgetCommandHandler : IRequestHandler<UpdateBudgetCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateBudgetCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateBudgetCommand request, CancellationToken cancellationToken)
    {
        var budget = await _context.Budgets
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (budget == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Budget), request.Id);
        }

        // Use domain method to update limit (includes validation)
        budget.UpdateLimit(request.LimitAmount);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
