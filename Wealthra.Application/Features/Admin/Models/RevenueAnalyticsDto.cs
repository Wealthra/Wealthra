namespace Wealthra.Application.Features.Admin.Models;

public record RevenueAnalyticsDto(
    decimal MonthlyRecurringRevenue,
    string MrrCurrency,
    int PayingSubscribers,
    decimal AverageRevenuePerUser);
