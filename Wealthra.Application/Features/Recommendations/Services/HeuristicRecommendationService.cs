using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Models;
using Wealthra.Domain.Entities;
using System.Globalization;

namespace Wealthra.Application.Features.Recommendations.Services
{
    public class HeuristicRecommendationService : IHeuristicRecommendationService
    {
        private const decimal HighIncomeShareThreshold = 20m;
        private const decimal MonthOverMonthSpikeRatio = 1.5m;

        public List<RecommendationSignal> Evaluate(IReadOnlyCollection<MonthlyCategoryMetric> metrics, string language)
        {
            var signals = new List<RecommendationSignal>();
            var normalizedLanguage = language?.Trim().ToLowerInvariant() ?? "en";
            var isTurkish = normalizedLanguage == "tr";

            foreach (var metric in metrics)
            {
                var categoryName = isTurkish && !string.IsNullOrWhiteSpace(metric.CategoryNameTr)
                    ? metric.CategoryNameTr
                    : metric.CategoryName;

                if (metric.SpendPercentageOfIncome >= HighIncomeShareThreshold)
                {
                    signals.Add(new RecommendationSignal
                    {
                        Source = "heuristic",
                        Severity = metric.SpendPercentageOfIncome > 30m ? "high" : "medium",
                        ReasonCode = "HIGH_INCOME_SHARE",
                        Evidence = isTurkish
                            ? $"'{categoryName}' harcaman gelirinin %{FormatPercentage(metric.SpendPercentageOfIncome)} seviyesinde."
                            : $"Your spending in '{categoryName}' is at %{FormatPercentage(metric.SpendPercentageOfIncome)} of your income.",
                        CategoryId = metric.CategoryId,
                        CategoryName = categoryName
                    });
                }

                if (metric.PreviousMonthSpend <= 0)
                {
                    continue;
                }

                var increaseRatio = metric.TotalSpend / metric.PreviousMonthSpend;
                if (increaseRatio >= MonthOverMonthSpikeRatio)
                {
                    var percentageIncrease = (increaseRatio - 1) * 100;
                    signals.Add(new RecommendationSignal
                    {
                        Source = "heuristic",
                        Severity = increaseRatio > 2m ? "high" : "medium",
                        ReasonCode = "MONTH_OVER_MONTH_SPIKE",
                        Evidence = isTurkish
                            ? $"'{categoryName}' harcamasi gecen aya gore %{FormatPercentage(percentageIncrease)} artti."
                            : $"Spending in '{categoryName}' increased by %{FormatPercentage(percentageIncrease)} compared to last month.",
                        CategoryId = metric.CategoryId,
                        CategoryName = categoryName
                    });
                }
            }

            return signals;
        }

        private static string FormatPercentage(decimal value)
            => Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);
    }
}
