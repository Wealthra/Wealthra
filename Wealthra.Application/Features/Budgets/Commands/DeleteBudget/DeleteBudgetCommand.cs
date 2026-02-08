using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Budgets.Commands.DeleteBudget;

public record DeleteBudgetCommand(int Id) : IRequest<Unit>;

public class DeleteBudgetCommandHandler : IRequestHandler<DeleteBudgetCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeleteBudgetCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(DeleteBudgetCommand request, CancellationToken cancellationToken)
    {
        var budget = await _context.Budgets
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (budget == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Budget), request.Id);
        }

        _context.Budgets.Remove(budget);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
