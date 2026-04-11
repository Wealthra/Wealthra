using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Application.Features.Expenses.Queries.GetUserExpenses;

public record GetUserExpensesQuery : IRequest<PaginatedList<ExpenseDto>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public class GetUserExpensesQueryHandler : IRequestHandler<GetUserExpensesQuery, PaginatedList<ExpenseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUserExpensesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<ExpenseDto>> Handle(GetUserExpensesQuery request, CancellationToken cancellationToken)
    {
        var expensesQuery = _context.Expenses
            .Include(e => e.Category)
            .Where(e => e.CreatedBy == _currentUserService.UserId)
            .OrderByDescending(e => e.TransactionDate)
            .Select(e => new ExpenseDto(
                e.Id,
                e.Description,
                e.Amount,
                e.PaymentMethod,
                e.IsRecurring,
                e.TransactionDate,
                e.CategoryId,
                e.Category.NameEn,
                e.Currency ?? "TRY"));

        return await PaginatedList<ExpenseDto>.CreateAsync(
            expensesQuery,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
