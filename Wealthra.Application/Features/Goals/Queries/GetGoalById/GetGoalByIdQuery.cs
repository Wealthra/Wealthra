using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Goals.Models;

namespace Wealthra.Application.Features.Goals.Queries.GetGoalById;

public record GetGoalByIdQuery(int Id) : IRequest<GoalDto>;

public class GetGoalByIdQueryHandler : IRequestHandler<GetGoalByIdQuery, GoalDto>
{
    private readonly IApplicationDbContext _context;

    public GetGoalByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GoalDto> Handle(GetGoalByIdQuery request, CancellationToken cancellationToken)
    {
        var goal = await _context.Goals
            .Where(g => g.Id == request.Id)
            .Select(g => new GoalDto(
                g.Id,
                g.Name,
                g.TargetAmount,
                g.CurrentAmount,
                g.TargetAmount > 0 ? (g.CurrentAmount / g.TargetAmount) * 100 : 0,
                g.Deadline,
                g.CurrentAmount >= g.TargetAmount))
            .FirstOrDefaultAsync(cancellationToken);

        if (goal == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Goal), request.Id);
        }

        return goal;
    }
}
