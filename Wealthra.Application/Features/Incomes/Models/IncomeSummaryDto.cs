namespace Wealthra.Application.Features.Incomes.Models;

public record IncomeSummaryDto(
    string Period,
    decimal TotalAmount,
    int IncomeCount);
