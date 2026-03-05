namespace Wealthra.Application.Features.Expenses.Models;

public record ExpenseGeneralInfoDto(
    decimal WeeklyTotal,
    decimal MonthlyTotal,
    decimal YearlyTotal,
    decimal RecurringExpensesThisMonth);
