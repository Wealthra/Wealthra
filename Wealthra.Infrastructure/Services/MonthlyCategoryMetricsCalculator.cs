using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;

namespace Wealthra.Infrastructure.Services;

public class MonthlyCategoryMetricsCalculator : IMonthlyCategoryMetricsCalculator
{
    private const string DefaultCurrency = "TRY";

    private readonly IApplicationDbContext _context;
    private readonly ICurrencyExchangeService _currencyService;

    public MonthlyCategoryMetricsCalculator(IApplicationDbContext context, ICurrencyExchangeService currencyService)
    {
        _context = context;
        _currencyService = currencyService;
    }

    public async Task<List<MonthlyCategoryMetric>> ComputeForMonthAsync(
        string userId,
        int year,
        int month,
        string targetCurrency,
        CancellationToken cancellationToken = default)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var prevStart = monthStart.AddMonths(-1);

        var totalIncome = await SumIncomesInCurrencyAsync(userId, monthStart, monthEnd, targetCurrency, cancellationToken).ConfigureAwait(false);
        var currentByCategory = await SumExpensesByCategoryAsync(userId, monthStart, monthEnd, targetCurrency, cancellationToken).ConfigureAwait(false);
        var prevByCategory = await SumExpensesByCategoryAsync(userId, prevStart, monthStart, targetCurrency, cancellationToken).ConfigureAwait(false);

        var list = new List<MonthlyCategoryMetric>();
        foreach (var kv in currentByCategory)
        {
            var categoryId = kv.Key;
            var row = kv.Value;
            var prevSpend = prevByCategory.TryGetValue(categoryId, out var p) ? p.Spent : 0m;
            var pct = totalIncome > 0 ? (row.Spent / totalIncome) * 100 : 0m;

            list.Add(new MonthlyCategoryMetric
            {
                UserId = userId,
                Month = monthStart,
                CategoryId = categoryId,
                CategoryName = row.NameEn,
                CategoryNameTr = row.NameTr,
                TotalSpend = row.Spent,
                TotalIncome = totalIncome,
                SpendPercentageOfIncome = pct,
                PreviousMonthSpend = prevSpend
            });
        }

        return list;
    }

    private async Task<decimal> SumIncomesInCurrencyAsync(
        string userId,
        DateTime fromInclusive,
        DateTime toExclusive,
        string targetCurrency,
        CancellationToken cancellationToken)
    {
        var incomes = await _context.Incomes
            .AsNoTracking()
            .Where(i => i.CreatedBy == userId && i.TransactionDate >= fromInclusive && i.TransactionDate < toExclusive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal sum = 0;
        foreach (var i in incomes)
        {
            var source = string.IsNullOrWhiteSpace(i.Currency) ? DefaultCurrency : i.Currency.ToUpperInvariant();
            var amt = i.Amount;
            if (!string.Equals(source, targetCurrency, StringComparison.Ordinal))
            {
                amt = await _currencyService.ConvertAsync(i.Amount, source, targetCurrency, cancellationToken).ConfigureAwait(false);
            }

            sum += amt;
        }

        return sum;
    }

    private async Task<Dictionary<int, (string NameEn, string NameTr, decimal Spent)>> SumExpensesByCategoryAsync(
        string userId,
        DateTime fromInclusive,
        DateTime toExclusive,
        string targetCurrency,
        CancellationToken cancellationToken)
    {
        var expenses = await _context.Expenses
            .AsNoTracking()
            .Include(e => e.Category)
            .Where(e => e.CreatedBy == userId && e.TransactionDate >= fromInclusive && e.TransactionDate < toExclusive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dict = new Dictionary<int, (string NameEn, string NameTr, decimal Spent)>();
        foreach (var e in expenses)
        {
            var source = string.IsNullOrWhiteSpace(e.Currency) ? DefaultCurrency : e.Currency.ToUpperInvariant();
            var amt = e.Amount;
            if (!string.Equals(source, targetCurrency, StringComparison.Ordinal))
            {
                amt = await _currencyService.ConvertAsync(e.Amount, source, targetCurrency, cancellationToken).ConfigureAwait(false);
            }

            if (!dict.TryGetValue(e.CategoryId, out var cur))
            {
                cur = (e.Category.NameEn, e.Category.NameTr, 0m);
            }

            dict[e.CategoryId] = (cur.NameEn, cur.NameTr, cur.Spent + amt);
        }

        return dict;
    }
}
