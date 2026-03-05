namespace Wealthra.Application.Features.Budgets.Models;

/// <summary>
/// Per-category budget progress for the current month.
/// </summary>
public record MonthlyBudgetCategoryDto(
    int BudgetId,
    string CategoryName,
    decimal LimitAmount,
    decimal SpentThisMonth,
    decimal RemainingAmount,
    decimal PercentageUsed,
    string Status);

/// <summary>
/// Overall monthly budget summary: aggregate of all category budgets.
/// </summary>
public record MonthlyBudgetSummaryDto(
    decimal TotalLimitAmount,
    decimal TotalSpentThisMonth,
    decimal TotalRemainingAmount,
    decimal OverallPercentageUsed,
    string OverallStatus,
    int TotalBudgets,
    int BudgetsExceeded,
    int BudgetsOnWarning,
    List<MonthlyBudgetCategoryDto> CategoryBreakdown);
