using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Identity.Commands.DeleteAccount;

public record DeleteAccountCommand : IRequest<Unit>
{
}

public class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;

    public DeleteAccountCommandHandler(
        IApplicationDbContext context,
        IIdentityService identityService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _identityService = identityService;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        // Delete user's data from Application DbContext
        var budgets = await _context.Budgets.Where(x => x.CreatedBy == userId).ToListAsync(cancellationToken);
        _context.Budgets.RemoveRange(budgets);

        var expenses = await _context.Expenses.Where(x => x.CreatedBy == userId).ToListAsync(cancellationToken);
        _context.Expenses.RemoveRange(expenses);

        var incomes = await _context.Incomes.Where(x => x.CreatedBy == userId).ToListAsync(cancellationToken);
        _context.Incomes.RemoveRange(incomes);

        var categories = await _context.Categories.Where(x => x.CreatedBy == userId).ToListAsync(cancellationToken);
        _context.Categories.RemoveRange(categories);

        var goals = await _context.Goals.Where(x => x.CreatedBy == userId).ToListAsync(cancellationToken);
        _context.Goals.RemoveRange(goals);

        var notifications = await _context.Notifications.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        _context.Notifications.RemoveRange(notifications);

        await _context.SaveChangesAsync(cancellationToken);

        // Delete user identity
        var result = await _identityService.DeleteUserAsync(userId);
        if (!result.Succeeded)
        {
            throw new Exception("Failed to delete user account: " + string.Join(", ", result.Errors));
        }

        return Unit.Value;
    }
}
