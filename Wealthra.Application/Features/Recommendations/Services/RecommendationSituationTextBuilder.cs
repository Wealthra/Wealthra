using System.Globalization;
using Wealthra.Application.Features.Recommendations.Models;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Features.Recommendations.Services
{
    /// <summary>
    /// Builds a single text blob for semantic tip search from heuristic signals and monthly metrics.
    /// </summary>
    public static class RecommendationSituationTextBuilder
    {
        private const int MaxMetricsInFallback = 5;

        public static string Build(
            IReadOnlyList<RecommendationSignal> signals,
            IReadOnlyList<MonthlyCategoryMetric> metrics,
            string language)
        {
            var normalizedLanguage = language?.Trim().ToLowerInvariant() ?? "en";
            var isTurkish = normalizedLanguage == "tr";

            var orderedSignals = signals
                .OrderByDescending(SeverityRank)
                .ThenBy(s => s.ReasonCode, StringComparer.Ordinal)
                .Select(s => s.Evidence.Trim())
                .Where(e => e.Length > 0)
                .ToList();

            if (orderedSignals.Count > 0)
            {
                return string.Join(" ", orderedSignals);
            }

            if (metrics.Count == 0)
            {
                return isTurkish
                    ? "Genel finansal düzen ve tasarruf ipuçları."
                    : "General budgeting and saving guidance.";
            }

            var parts = new List<string>();
            var monthLabel = metrics[0].Month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            parts.Add(isTurkish
                ? $"Ay {monthLabel} özeti."
                : $"Month {monthLabel} summary.");

            var incomeSample = metrics.Max(m => m.TotalIncome);
            if (incomeSample > 0)
            {
                parts.Add(isTurkish
                    ? $"Toplam gelir yaklaşık {FormatAmount(incomeSample)}."
                    : $"Total income about {FormatAmount(incomeSample)}.");
            }

            foreach (var m in metrics
                         .OrderByDescending(x => x.SpendPercentageOfIncome)
                         .ThenByDescending(x => x.TotalSpend)
                         .Take(MaxMetricsInFallback))
            {
                var name = isTurkish && !string.IsNullOrWhiteSpace(m.CategoryNameTr)
                    ? m.CategoryNameTr
                    : m.CategoryName;
                parts.Add(isTurkish
                    ? $"'{name}' harcaması {FormatAmount(m.TotalSpend)}, gelirin %{FormatPct(m.SpendPercentageOfIncome)} kadarı."
                    : $"'{name}' spending {FormatAmount(m.TotalSpend)}, about {FormatPct(m.SpendPercentageOfIncome)}% of income.");
            }

            return string.Join(" ", parts);
        }

        private static int SeverityRank(RecommendationSignal s) =>
            s.Severity?.ToLowerInvariant() switch
            {
                "high" => 3,
                "medium" => 2,
                "info" => 1,
                _ => 0
            };

        private static string FormatAmount(decimal value) =>
            Math.Round(value, 0).ToString("0", CultureInfo.InvariantCulture);

        private static string FormatPct(decimal value) =>
            Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);
    }
}
