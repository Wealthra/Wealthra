namespace Wealthra.Application.Features.Statistics.Models;

public record MonthlyTrendsDto(
    int Year,
    List<MonthlyTrendItem> MonthlyData);

public record MonthlyTrendItem(
    int Month,
    string MonthName,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetAmount);
