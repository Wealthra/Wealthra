using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Statistics.Models;

namespace Wealthra.Application.Features.Statistics.Queries.GetSpendingBreakdown;

public record GetSpendingBreakdownQuery : IRequest<SpendingBreakdownDto>
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public class GetSpendingBreakdownQueryValidator : AbstractValidator<GetSpendingBreakdownQuery>
{
    public GetSpendingBreakdownQueryValidator()
    {
        RuleFor(v => v)
            .Must(v => !v.StartDate.HasValue || !v.EndDate.HasValue || v.StartDate.Value <= v.EndDate.Value)
            .WithMessage("StartDate must be before or equal to EndDate.");
    }
}

public class GetSpendingBreakdownQueryHandler : IRequestHandler<GetSpendingBreakdownQuery, SpendingBreakdownDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSpendingBreakdownQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<SpendingBreakdownDto> Handle(GetSpendingBreakdownQuery request, CancellationToken cancellationToken)
    {
        // Default to current month if no date range provided
        var startDate = request.StartDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var endDate = request.EndDate ?? DateTime.UtcNow;

        // Get expenses within date range
        var expenses = await _context.Expenses
            .Include(e => e.Category)
            .Where(e => e.CreatedBy == _currentUserService.UserId &&
                       e.TransactionDate >= startDate &&
                       e.TransactionDate <= endDate)
            .ToListAsync(cancellationToken);

        var totalAmount = expenses.Sum(e => e.Amount);

        // Group by category
        var categoryBreakdown = expenses
            .GroupBy(e => e.CategoryId)
            .Select(g => new CategoryBreakdownItem(
                g.First().Category.NameEn,
                g.Sum(e => e.Amount),
                totalAmount > 0 ? (g.Sum(e => e.Amount) / totalAmount) * 100 : 0,
                g.Count()))
            .OrderByDescending(c => c.Amount)
            .ToList();

        return new SpendingBreakdownDto(
            categoryBreakdown,
            totalAmount,
            startDate,
            endDate);
    }
}
