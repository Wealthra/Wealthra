namespace Wealthra.Application.Features.Statistics.Models;

public record SpendingBreakdownDto(
    List<CategoryBreakdownItem> CategoryBreakdown,
    decimal TotalAmount,
    DateTime StartDate,
    DateTime EndDate);

public record CategoryBreakdownItem(
    string CategoryNameEn,
    string CategoryNameTr,
    decimal Amount,
    decimal Percentage,
    int TransactionCount);
