using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Categories.Models;
using Wealthra.Application.Features.Incomes.Models;

namespace Wealthra.Application.Features.Incomes.Queries.GetIncomeSummary;

public record GetIncomeSummaryQuery : IRequest<List<IncomeSummaryDto>>
{
    public string Period { get; init; } = "Monthly";
    public string? TargetCurrency { get; init; }
    public string Language { get; init; } = "en";
}

public class GetIncomeSummaryQueryValidator : AbstractValidator<GetIncomeSummaryQuery>
{
    public GetIncomeSummaryQueryValidator()
    {
        RuleFor(v => v.Period)
            .Must(p => p == "Weekly" || p == "Monthly" || p == "Yearly")
            .WithMessage("Period must be Weekly, Monthly, or Yearly.");

        RuleFor(v => v.Language)
            .Must(l => CategoryLanguageParser.TryParse(l, out _))
            .WithMessage("Invalid language. Use 'en' or 'tr'.");
    }
}

public class GetIncomeSummaryQueryHandler : IRequestHandler<GetIncomeSummaryQuery, List<IncomeSummaryDto>>
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly ICurrencyExchangeService _currencyService;

    public GetIncomeSummaryQueryHandler(
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

    public async Task<List<IncomeSummaryDto>> Handle(GetIncomeSummaryQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUserService.UserId;

        var userDetails = await _identityService.GetUserDetailsAsync(userId!);
        var prefCurrency = request.TargetCurrency?.ToUpperInvariant()
            ?? userDetails?.PreferredCurrency?.ToUpperInvariant()
            ?? DefaultCurrency;

        var incomes = await _context.Incomes
            .AsNoTracking()
            .Where(i => i.CreatedBy == userId)
            .ToListAsync(cancellationToken);

        foreach (var i in incomes)
        {
            var sourceCurrency = string.IsNullOrWhiteSpace(i.Currency)
                ? DefaultCurrency
                : i.Currency.ToUpperInvariant();
            i.Amount = await _currencyService.ConvertAsync(i.Amount, sourceCurrency, prefCurrency, cancellationToken);
        }

        CategoryLanguageParser.TryParse(request.Language, out var categoryLanguage);
        var useTr = categoryLanguage == CategoryDisplayLanguage.Turkish;
        var culture = useTr ? new CultureInfo("tr-TR") : new CultureInfo("en-US");

        var groupedIncomes = request.Period switch
        {
            "Weekly" => GroupByWeek(incomes, now, useTr, culture),
            "Monthly" => GroupByMonth(incomes, now, culture),
            "Yearly" => GroupByYear(incomes, now),
            _ => GroupByMonth(incomes, now, culture)
        };

        return groupedIncomes;
    }

    private List<IncomeSummaryDto> GroupByWeek(List<Domain.Entities.Income> incomes, DateTime now, bool useTr, CultureInfo culture)
    {
        var last12Weeks = Enumerable.Range(0, 12)
            .Select(i => now.AddDays(-i * 7).Date)
            .Reverse()
            .ToList();

        return last12Weeks.Select(weekStart =>
        {
            var weekEnd = weekStart.AddDays(7);
            var weekIncomes = incomes.Where(i => i.TransactionDate >= weekStart && i.TransactionDate < weekEnd).ToList();

            var label = useTr
                ? $"{weekStart.ToString("dd MMM", culture)} Haftası"
                : $"Week of {weekStart.ToString("MMM dd", culture)}";

            return new IncomeSummaryDto(
                label,
                weekIncomes.Sum(i => i.Amount),
                weekIncomes.Count
            );
        }).ToList();
    }

    private List<IncomeSummaryDto> GroupByMonth(List<Domain.Entities.Income> incomes, DateTime now, CultureInfo culture)
    {
        var last12Months = Enumerable.Range(0, 12)
            .Select(i => now.AddMonths(-i).Date)
            .Reverse()
            .ToList();

        return last12Months.Select(monthStart =>
        {
            var monthIncomes = incomes.Where(i =>
                i.TransactionDate.Year == monthStart.Year &&
                i.TransactionDate.Month == monthStart.Month).ToList();

            return new IncomeSummaryDto(
                monthStart.ToString("MMMM yyyy", culture),
                monthIncomes.Sum(i => i.Amount),
                monthIncomes.Count
            );
        }).ToList();
    }

    private List<IncomeSummaryDto> GroupByYear(List<Domain.Entities.Income> incomes, DateTime now)
    {
        var last5Years = Enumerable.Range(0, 5)
            .Select(i => now.AddYears(-i).Year)
            .Reverse()
            .ToList();

        return last5Years.Select(year =>
        {
            var yearIncomes = incomes.Where(i => i.TransactionDate.Year == year).ToList();

            return new IncomeSummaryDto(
                year.ToString(),
                yearIncomes.Sum(i => i.Amount),
                yearIncomes.Count
            );
        }).ToList();
    }
}
