using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Budgets.Models;

namespace Wealthra.Application.Features.Budgets.Queries.GetUserBudgets;

public record GetUserBudgetsQuery : IRequest<List<BudgetDto>>;

public class GetUserBudgetsQueryHandler : IRequestHandler<GetUserBudgetsQuery, List<BudgetDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUserBudgetsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<BudgetDto>> Handle(GetUserBudgetsQuery request, CancellationToken cancellationToken)
    {
        var budgets = await _context.Budgets
            .Include(b => b.Category)
            .Where(b => b.CreatedBy == _currentUserService.UserId)
            .Select(b => new BudgetDto(
                b.Id,
                b.LimitAmount,
                b.CurrentAmount,
                b.LimitAmount > 0 ? (b.CurrentAmount / b.LimitAmount) * 100 : 0,
                GetBudgetStatus(b.CurrentAmount, b.LimitAmount),
                b.CategoryId,
                b.Category.NameEn,
                b.Category.NameTr))
            .ToListAsync(cancellationToken);

        return budgets;
    }

    private static string GetBudgetStatus(decimal currentAmount, decimal limitAmount)
    {
        if (limitAmount == 0) return "Safe";

        var percentage = (currentAmount / limitAmount) * 100;

        return percentage switch
        {
            >= 100 => "Exceeded",
            >= 80 => "Warning",
            _ => "Safe"
        };
    }
}
