using Wealthra.Application.Features.Budgets.Models;
using Wealthra.Application.Features.Expenses.Models;
using Wealthra.Application.Features.Goals.Models;
using Wealthra.Application.Features.Incomes.Models;

namespace Wealthra.Application.Features.Export.Models;

public record FinancialReportData(
    DateTime StartDate,
    DateTime EndDate,
    string Currency,
    List<ExpenseDto> Expenses,
    List<IncomeDto> Incomes,
    List<BudgetDto> Budgets,
    List<GoalDto> Goals
);