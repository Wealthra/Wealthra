using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Categories.Models;
using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Application.Features.Expenses.Queries.GetExpenseSummary;

public record GetExpenseSummaryQuery : IRequest<List<ExpenseSummaryDto>>
{
    public string Period { get; init; } = "Monthly";
    public string? TargetCurrency { get; init; }
    public string Language { get; init; } = "en";
}

public class GetExpenseSummaryQueryValidator : AbstractValidator<GetExpenseSummaryQuery>
{
    public GetExpenseSummaryQueryValidator()
    {
        RuleFor(v => v.Period)
            .Must(p => p == "Weekly" || p == "Monthly" || p == "Yearly")
            .WithMessage("Period must be Weekly, Monthly, or Yearly.");

        RuleFor(v => v.Language)
            .Must(l => CategoryLanguageParser.TryParse(l, out _))
            .WithMessage("Invalid language. Use 'en' or 'tr'.");
    }
}

public class GetExpenseSummaryQueryHandler : IRequestHandler<GetExpenseSummaryQuery, List<ExpenseSummaryDto>>
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly ICurrencyExchangeService _currencyService;

    public GetExpenseSummaryQueryHandler(
        IApplicationDbContext context, 
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        ICurrencyExchangeService currencyService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _currencyService = currencyService;
    }

    public async Task<List<ExpenseSummaryDto>> Handle(GetExpenseSummaryQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUserService.UserId;

        var userDetails = await _identityService.GetUserDetailsAsync(userId!);
        var prefCurrency = request.TargetCurrency?.ToUpperInvariant()
            ?? userDetails?.PreferredCurrency?.ToUpperInvariant()
            ?? DefaultCurrency;

        var expenses = await _context.Expenses
            .AsNoTracking()
            .Include(e => e.Category)
            .Where(e => e.CreatedBy == userId)
            .ToListAsync(cancellationToken);

        foreach (var e in expenses)
        {
            var sourceCurrency = string.IsNullOrWhiteSpace(e.Currency)
                ? DefaultCurrency
                : e.Currency.ToUpperInvariant();
            e.Amount = await _currencyService.ConvertAsync(e.Amount, sourceCurrency, prefCurrency, cancellationToken);
        }

        CategoryLanguageParser.TryParse(request.Language, out var categoryLanguage);
        var useTr = categoryLanguage == CategoryDisplayLanguage.Turkish;
        var culture = useTr ? new CultureInfo("tr-TR") : new CultureInfo("en-US");

        var groupedExpenses = request.Period switch
        {
            "Weekly" => GroupByWeek(expenses, now, useTr, culture),
            "Monthly" => GroupByMonth(expenses, now, useTr, culture),
            "Yearly" => GroupByYear(expenses, now, useTr),
            _ => GroupByMonth(expenses, now, useTr, culture)
        };

        return groupedExpenses;
    }

    private static string CategoryLabel(Domain.Entities.Expense e, bool useTr) =>
        useTr ? e.Category.NameTr : e.Category.NameEn;

    private List<ExpenseSummaryDto> GroupByWeek(List<Domain.Entities.Expense> expenses, DateTime now, bool useTr, CultureInfo culture)
    {
        var last12Weeks = Enumerable.Range(0, 12)
            .Select(i => now.AddDays(-i * 7).Date)
            .Reverse()
            .ToList();

        return last12Weeks.Select(weekStart =>
        {
            var weekEnd = weekStart.AddDays(7);
            var weekExpenses = expenses.Where(e => e.TransactionDate >= weekStart && e.TransactionDate < weekEnd).ToList();

            var label = useTr
                ? $"{weekStart.ToString("dd MMM", culture)} Haftası"
                : $"Week of {weekStart.ToString("MMM dd", culture)}";

            return new ExpenseSummaryDto(
                label,
                weekExpenses.Sum(e => e.Amount),
                weekExpenses.Count,
                weekExpenses.GroupBy(e => CategoryLabel(e, useTr))
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount))
            );
        }).ToList();
    }

    private List<ExpenseSummaryDto> GroupByMonth(List<Domain.Entities.Expense> expenses, DateTime now, bool useTr, CultureInfo culture)
    {
        var last12Months = Enumerable.Range(0, 12)
            .Select(i => now.AddMonths(-i).Date)
            .Reverse()
            .ToList();

        return last12Months.Select(monthStart =>
        {
            var monthExpenses = expenses.Where(e =>
                e.TransactionDate.Year == monthStart.Year &&
                e.TransactionDate.Month == monthStart.Month).ToList();

            return new ExpenseSummaryDto(
                monthStart.ToString("MMMM yyyy", culture),
                monthExpenses.Sum(e => e.Amount),
                monthExpenses.Count,
                monthExpenses.GroupBy(e => CategoryLabel(e, useTr))
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount))
            );
        }).ToList();
    }

    private List<ExpenseSummaryDto> GroupByYear(List<Domain.Entities.Expense> expenses, DateTime now, bool useTr)
    {
        var last5Years = Enumerable.Range(0, 5)
            .Select(i => now.AddYears(-i).Year)
            .Reverse()
            .ToList();

        return last5Years.Select(year =>
        {
            var yearExpenses = expenses.Where(e => e.TransactionDate.Year == year).ToList();

            return new ExpenseSummaryDto(
                year.ToString(),
                yearExpenses.Sum(e => e.Amount),
                yearExpenses.Count,
                yearExpenses.GroupBy(e => CategoryLabel(e, useTr))
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount))
            );
        }).ToList();
    }
}
