namespace Wealthra.Application.Features.FinancialSummary.Models;

public record DashboardWebDto(
    DashboardWebSummaryDto Summary,
    DashboardWebChartsDto Charts,
    DashboardWebListsDto Lists,
    DashboardWebGoalsOverviewDto GoalsOverview,
    List<DashboardWebRecommendationDto> Recommendations);

public record DashboardWebSummaryDto(
    decimal TotalBalance,
    decimal TotalIncome,
    decimal TotalExpenses,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal MonthlyCashFlow,
    decimal SavingsRate,
    int ActiveBudgetsCount,
    DashboardWebGoalsCountDto GoalsCount,
    int UnreadNotificationsCount);

public record DashboardWebGoalsCountDto(int Total, int Achieved);

public record DashboardWebChartsDto(
    DashboardWebIncomeExpenseTrendDto IncomeExpenseTrend,
    DashboardWebSpendingsBreakdownDto SpendingsBreakdown);

public record DashboardWebIncomeExpenseTrendDto(
    string Granularity,
    List<DashboardWebIncomeExpensePointDto> Points);

public record DashboardWebIncomeExpensePointDto(
    string Label,
    decimal Income,
    decimal Expense);

public record DashboardWebSpendingsBreakdownDto(
    string GroupBy,
    decimal TotalAmount,
    List<DashboardWebSpendingCategoryDto> Categories);

public record DashboardWebSpendingCategoryDto(
    string CategoryName,
    decimal TotalAmount,
    int TransactionCount,
    decimal Percentage);

public record DashboardWebListsDto(
    List<DashboardWebRecentTransactionDto> RecentTransactions,
    List<DashboardWebTopCategoryDto> TopSpendingCategories,
    List<DashboardWebBudgetAlertDto> BudgetAlerts);

public record DashboardWebRecentTransactionDto(
    int Id,
    string Type,
    string Description,
    decimal Amount,
    DateTime TransactionDate,
    string? CategoryName,
    bool IsRecurring,
    string? MerchantName);

public record DashboardWebTopCategoryDto(
    string CategoryName,
    decimal TotalAmount,
    int TransactionCount);

public record DashboardWebBudgetAlertDto(
    int BudgetId,
    string CategoryName,
    decimal LimitAmount,
    decimal CurrentAmount,
    decimal PercentageUsed,
    string Status);

public record DashboardWebGoalsOverviewDto(
    int TotalGoals,
    int AchievedGoals,
    decimal CurrentAmount,
    decimal LimitAmount);

public record DashboardWebRecommendationDto(
    string Id,
    string Type,
    string Title,
    string Description,
    string? RelatedCategory,
    string Severity);
