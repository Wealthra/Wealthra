namespace Wealthra.Application.Features.Expenses.Models;

public record ExpenseDto(
    int Id,
    string Description,
    decimal Amount,
    string PaymentMethod,
    bool IsRecurring,
    DateTime TransactionDate,
    int CategoryId,
    string CategoryNameEn,
    string CategoryNameTr);
