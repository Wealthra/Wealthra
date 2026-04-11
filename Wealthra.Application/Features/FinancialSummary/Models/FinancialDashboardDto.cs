namespace Wealthra.Application.Features.FinancialSummary.Models;

public record FinancialDashboardDto(
    decimal TotalBalance,
    decimal TotalIncome,
    decimal TotalExpenses,
    List<RecentTransactionDto> RecentTransactions,
    List<TopCategoryDto> TopSpendingCategories,
    List<BudgetAlertDto> BudgetAlerts,
    int UnreadNotificationsCount,
    string Currency);

public record RecentTransactionDto(
    int Id,
    string Type,
    string Description,
    decimal Amount,
    string Currency,
    DateTime TransactionDate,
    string? CategoryName);

public record TopCategoryDto(
    string CategoryName,
    decimal TotalAmount,
    string Currency,
    int TransactionCount);

public record BudgetAlertDto(
    int BudgetId,
    string CategoryName,
    decimal LimitAmount,
    decimal CurrentAmount,
    string Currency,
    decimal PercentageUsed,
    string Status);
