namespace Wealthra.Application.Features.FinancialSummary.Models;

public record FinancialDashboardDto(
    decimal TotalBalance,
    decimal TotalIncome,
    decimal TotalExpenses,
    List<RecentTransactionDto> RecentTransactions,
    List<TopCategoryDto> TopSpendingCategories,
    List<BudgetAlertDto> BudgetAlerts,
    int UnreadNotificationsCount);

public record RecentTransactionDto(
    int Id,
    string Type,
    string Description,
    decimal Amount,
    DateTime TransactionDate,
    string? CategoryName);

public record TopCategoryDto(
    string CategoryName,
    decimal TotalAmount,
    int TransactionCount);

public record BudgetAlertDto(
    int BudgetId,
    string CategoryName,
    decimal LimitAmount,
    decimal CurrentAmount,
    decimal PercentageUsed,
    string Status);
