using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Application.Features.Expenses.Queries.GetExpenses;

public record GetExpensesQuery : IRequest<PaginatedList<ExpenseDto>>
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int? CategoryId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public class GetExpensesQueryHandler : IRequestHandler<GetExpensesQuery, PaginatedList<ExpenseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetExpensesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<ExpenseDto>> Handle(GetExpensesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Expenses
            .Include(e => e.Category)
            .Where(e => e.CreatedBy == _currentUserService.UserId)
            .AsQueryable();

        if (request.StartDate.HasValue)
        {
            query = query.Where(e => e.TransactionDate >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(e => e.TransactionDate <= request.EndDate.Value);
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(e => e.CategoryId == request.CategoryId.Value);
        }

        var orderedQuery = query.OrderByDescending(e => e.TransactionDate);

        var expensesQuery = orderedQuery.Select(e => new ExpenseDto(
            e.Id,
            e.Description,
            e.Amount,
            e.PaymentMethod,
            e.IsRecurring,
            e.TransactionDate,
            e.CategoryId,
            e.Category.NameEn,
            e.Category.NameTr));

        return await PaginatedList<ExpenseDto>.CreateAsync(
            expensesQuery,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
