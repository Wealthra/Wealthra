namespace Wealthra.Application.Features.Incomes.Models;

public record IncomeGeneralInfoDto(
    decimal WeeklyTotal,
    decimal MonthlyTotal,
    decimal YearlyTotal,
    decimal AverageMonthlyIncome);
