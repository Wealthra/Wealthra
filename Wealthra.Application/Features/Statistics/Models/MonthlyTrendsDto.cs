namespace Wealthra.Application.Features.Statistics.Models;

public record MonthlyTrendsDto(
    int Year,
    List<MonthlyTrendItem> MonthlyData,
    string Currency);

public record MonthlyTrendItem(
    int Month,
    string MonthName,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetAmount);
