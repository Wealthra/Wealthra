namespace Wealthra.Application.Features.Incomes.Models;

public record IncomeDto(
    int Id,
    string Name,
    decimal Amount,
    string Method,
    bool IsRecurring,
    DateTime TransactionDate);
