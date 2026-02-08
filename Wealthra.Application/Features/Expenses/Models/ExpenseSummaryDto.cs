namespace Wealthra.Application.Features.Expenses.Models;

public record ExpenseSummaryDto(
    string Period,
    decimal TotalAmount,
    int ExpenseCount,
    Dictionary<string, decimal> CategoryBreakdown);
