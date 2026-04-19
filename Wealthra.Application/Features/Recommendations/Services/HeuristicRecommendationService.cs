using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Models;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Recommendations.Services
{
    public class HeuristicRecommendationService : IHeuristicRecommendationService
    {
        private const decimal HighIncomeShareThreshold = 20m;
        private const decimal MonthOverMonthSpikeRatio = 1.5m;

        public List<RecommendationSignal> Evaluate(IReadOnlyCollection<MonthlyCategoryMetric> metrics)
        {
            var signals = new List<RecommendationSignal>();

            foreach (var metric in metrics)
            {
                if (metric.SpendPercentageOfIncome >= HighIncomeShareThreshold)
                {
                    signals.Add(new RecommendationSignal
                    {
                        Source = "heuristic",
                        Severity = metric.SpendPercentageOfIncome > 30m ? "high" : "medium",
                        ReasonCode = "HIGH_INCOME_SHARE",
                        Evidence = $"'{metric.CategoryName}' harcaman gelirinin %{Math.Round(metric.SpendPercentageOfIncome, 1)} seviyesinde.",
                        CategoryId = metric.CategoryId,
                        CategoryName = metric.CategoryName
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
                        Evidence = $"'{metric.CategoryName}' harcaması geçen aya göre %{Math.Round(percentageIncrease, 1)} arttı.",
                        CategoryId = metric.CategoryId,
                        CategoryName = metric.CategoryName
                    });
                }
            }

            return signals;
        }
    }
}
