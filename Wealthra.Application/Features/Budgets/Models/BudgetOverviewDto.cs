namespace Wealthra.Application.Features.Budgets.Models;

public record BudgetOverviewDto(
    decimal TotalLimit,
    decimal TotalSpent,
    decimal PercentageUsed,
    string OverallStatus,
    int TotalBudgets,
    int BudgetsExceeded,
    int BudgetsWarning);
