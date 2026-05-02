using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Caching;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Incomes.Commands.DeleteIncome;

public record DeleteIncomeCommand(int Id) : IRequest<Unit>;

public class DeleteIncomeCommandHandler : IRequestHandler<DeleteIncomeCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public DeleteIncomeCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
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

        await FinancialDashboardCache.InvalidateForUserAsync(_cacheService, _currentUserService.UserId!, cancellationToken);

        return Unit.Value;
    }
}
