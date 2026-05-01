using System.Globalization;
using Wealthra.Application.Features.FinancialSummary.Models;

namespace Wealthra.Application.Features.FinancialSummary;

/// <summary>
/// Builds dashboard recommendation DTOs and formats monetary amounts for display (invariant culture, 2 decimal places).
/// </summary>
public static class DashboardWebRecommendations
{
    public static string FormatMoneyForDisplay(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture);

    public static List<DashboardWebRecommendationDto> BuildFromBudgetAlerts(
        List<DashboardWebBudgetAlertDto> alerts,
        string currency)
    {
        var list = new List<DashboardWebRecommendationDto>();
        foreach (var a in alerts.Take(10))
        {
            var over = a.CurrentAmount - a.LimitAmount;
            var title = a.Status == "Exceeded"
                ? $"You exceeded your {a.CategoryName} budget"
                : $"You're close to your {a.CategoryName} budget";

            string description;
            if (a.Status == "Exceeded" && over > 0)
            {
                description =
                    $"You are {FormatMoneyForDisplay(over)} {currency} over your limit. Consider reducing {a.CategoryName} spend next month.";
            }
            else
            {
                var room = a.LimitAmount - a.CurrentAmount;
                description = room >= 0
                    ? $"Leave about {FormatMoneyForDisplay(room)} {currency} in this category to stay within your limit."
                    : "Review this category to avoid overspending.";
            }

            var severity = a.Status == "Exceeded" ? "high" : "medium";
            list.Add(new DashboardWebRecommendationDto(
                $"budget_{a.BudgetId}",
                "spending_insight",
                title,
                description,
                a.CategoryName,
                severity));
        }

        return list;
    }
}
