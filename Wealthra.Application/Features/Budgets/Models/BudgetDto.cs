namespace Wealthra.Application.Features.Budgets.Models;

public record BudgetDto(
    int Id,
    decimal LimitAmount,
    decimal CurrentAmount,
    decimal PercentageUsed,
    string Status,
    int CategoryId,
    string CategoryName);
