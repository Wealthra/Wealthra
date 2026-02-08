using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Incomes.Commands.DeleteIncome;

public record DeleteIncomeCommand(int Id) : IRequest<Unit>;

public class DeleteIncomeCommandHandler : IRequestHandler<DeleteIncomeCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeleteIncomeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(DeleteIncomeCommand request, CancellationToken cancellationToken)
    {
        var income = await _context.Incomes
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (income == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Income), request.Id);
        }

        _context.Incomes.Remove(income);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
