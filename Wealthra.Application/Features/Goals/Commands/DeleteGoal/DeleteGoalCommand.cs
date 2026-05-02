using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Caching;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Goals.Commands.DeleteGoal;

public record DeleteGoalCommand(int Id) : IRequest<Unit>;

public class DeleteGoalCommandHandler : IRequestHandler<DeleteGoalCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public DeleteGoalCommandHandler(IApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<Unit> Handle(DeleteGoalCommand request, CancellationToken cancellationToken)
    {
        var goal = await _context.Goals
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);

        if (goal == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Goal), request.Id);
        }

        _context.Goals.Remove(goal);
        await _context.SaveChangesAsync(cancellationToken);

        await FinancialDashboardCache.InvalidateForUserAsync(_cacheService, goal.CreatedBy, cancellationToken);

        return Unit.Value;
    }
}
