using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Budgets.Models;
using Wealthra.Application.Features.Expenses.Models;
using Wealthra.Application.Features.Export.Models;
using Wealthra.Application.Features.Goals.Models;
using Wealthra.Application.Features.Incomes.Models;

namespace Wealthra.Application.Features.Export.Queries.ExportFinancialData;

public record ExportFinancialDataQuery : IRequest<ExportFileDto>
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string Format { get; init; } = "pdf"; // "pdf" or "excel"
    public string? TargetCurrency { get; init; }
}

public class ExportFinancialDataQueryValidator : AbstractValidator<ExportFinancialDataQuery>
{
    public ExportFinancialDataQueryValidator()
    {
        RuleFor(x => x.Format)
            .Must(f => f.ToLower() == "pdf" || f.ToLower() == "excel")
            .WithMessage("Format must be 'pdf' or 'excel'.");
    }
}

public class ExportFinancialDataQueryHandler : IRequestHandler<ExportFinancialDataQuery, ExportFileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly ICurrencyExchangeService _currencyService;
    private readonly IReportExportService _exportService;

    public ExportFinancialDataQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        ICurrencyExchangeService currencyService,
        IReportExportService exportService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _currencyService = currencyService;
        _exportService = exportService;
    }

    public async Task<ExportFileDto> Handle(ExportFinancialDataQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var userDetails = await _identityService.GetUserDetailsAsync(userId!);
        var targetCurrency = request.TargetCurrency ?? userDetails?.PreferredCurrency ?? "TRY";

        var startDate = request.StartDate ?? new DateTime(DateTime.UtcNow.Year, 1, 1);
        var endDate = request.EndDate ?? DateTime.UtcNow;

        // Fetch Data
        var expenses = await _context.Expenses.Include(e => e.Category)
            .Where(e => e.CreatedBy == userId && e.TransactionDate >= startDate && e.TransactionDate <= endDate)
            .ToListAsync(cancellationToken);

        var incomes = await _context.Incomes
            .Where(i => i.CreatedBy == userId && i.TransactionDate >= startDate && i.TransactionDate <= endDate)
            .ToListAsync(cancellationToken);

        var budgets = await _context.Budgets.Include(b => b.Category)
            .Where(b => b.CreatedBy == userId)
            .ToListAsync(cancellationToken);

        var goals = await _context.Goals
            .Where(g => g.CreatedBy == userId)
            .ToListAsync(cancellationToken);

        // Convert Currencies
        var expenseDtos = new List<ExpenseDto>();
        foreach (var e in expenses)
        {
            var amount = await _currencyService.ConvertAsync(e.Amount, e.Currency ?? "TRY", targetCurrency, cancellationToken);
            expenseDtos.Add(new ExpenseDto(e.Id, e.Description, amount, e.PaymentMethod, e.IsRecurring, e.TransactionDate, e.CategoryId, e.Category.NameEn, targetCurrency));
        }

        var incomeDtos = new List<IncomeDto>();
        foreach (var i in incomes)
        {
            var amount = await _currencyService.ConvertAsync(i.Amount, i.Currency ?? "TRY", targetCurrency, cancellationToken);
            incomeDtos.Add(new IncomeDto(i.Id, i.Name, amount, i.Method, i.IsRecurring, i.TransactionDate, targetCurrency));
        }

        var budgetDtos = new List<BudgetDto>();
        foreach (var b in budgets)
        {
            var limit = await _currencyService.ConvertAsync(b.LimitAmount, b.Currency ?? "TRY", targetCurrency, cancellationToken);
            var current = await _currencyService.ConvertAsync(b.CurrentAmount, b.Currency ?? "TRY", targetCurrency, cancellationToken);
            budgetDtos.Add(new BudgetDto(b.Id, limit, current, limit > 0 ? (current/limit)*100 : 0, "Active", b.CategoryId, b.Category.NameEn));
        }

        var goalDtos = new List<GoalDto>();
        foreach (var g in goals)
        {
            var target = await _currencyService.ConvertAsync(g.TargetAmount, g.Currency ?? "TRY", targetCurrency, cancellationToken);
            var current = await _currencyService.ConvertAsync(g.CurrentAmount, g.Currency ?? "TRY", targetCurrency, cancellationToken);
            goalDtos.Add(new GoalDto(g.Id, g.Name, target, current, target > 0 ? (current/target)*100 : 0, g.Deadline, current >= target));
        }

        var reportData = new FinancialReportData(startDate, endDate, targetCurrency, expenseDtos, incomeDtos, budgetDtos, goalDtos);

        if (request.Format.ToLower() == "excel")
        {
            var excelBytes = _exportService.GenerateExcelReport(reportData);
            return new ExportFileDto($"Wealthra_Report_{DateTime.UtcNow:yyyyMMdd}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelBytes);
        }
        else
        {
            var pdfBytes = _exportService.GeneratePdfReport(reportData);
            return new ExportFileDto($"Wealthra_Report_{DateTime.UtcNow:yyyyMMdd}.pdf", "application/pdf", pdfBytes);
        }
    }
}