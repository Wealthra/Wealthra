using FluentAssertions;
using Wealthra.Application.Features.Recommendations.Models;
using Wealthra.Application.Features.Recommendations.Services;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.UnitTests.Features.Recommendations.Services
{
    public class RecommendationSituationTextBuilderTests
    {
        private static readonly DateTime Month = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Build_WithSignals_JoinsEvidenceOrderedBySeverity()
        {
            var signals = new List<RecommendationSignal>
            {
                new()
                {
                    Severity = "medium",
                    ReasonCode = "B",
                    Evidence = "Medium B."
                },
                new()
                {
                    Severity = "high",
                    ReasonCode = "A",
                    Evidence = "High A."
                }
            };

            var text = RecommendationSituationTextBuilder.Build(signals, Array.Empty<MonthlyCategoryMetric>(), "en");

            text.Should().Be("High A. Medium B.");
        }

        [Fact]
        public void Build_WithNoSignals_UsesMetricFallback_En()
        {
            var metrics = new List<MonthlyCategoryMetric>
            {
                new()
                {
                    UserId = "u",
                    Month = Month,
                    CategoryId = 1,
                    CategoryName = "Food",
                    CategoryNameTr = "Gida",
                    TotalSpend = 800,
                    TotalIncome = 4000,
                    SpendPercentageOfIncome = 20,
                    PreviousMonthSpend = 0
                }
            };

            var text = RecommendationSituationTextBuilder.Build(Array.Empty<RecommendationSignal>(), metrics, "en");

            text.Should().Contain("2026-04");
            text.Should().Contain("Food");
            text.Should().Contain("800");
            text.Should().Contain("20");
        }

        [Fact]
        public void Build_WithNoSignals_UsesMetricFallback_Tr()
        {
            var metrics = new List<MonthlyCategoryMetric>
            {
                new()
                {
                    UserId = "u",
                    Month = Month,
                    CategoryId = 1,
                    CategoryName = "Food",
                    CategoryNameTr = "Gida",
                    TotalSpend = 800,
                    TotalIncome = 4000,
                    SpendPercentageOfIncome = 20,
                    PreviousMonthSpend = 0
                }
            };

            var text = RecommendationSituationTextBuilder.Build(Array.Empty<RecommendationSignal>(), metrics, "tr");

            text.Should().Contain("Gida");
            text.Should().Contain("özeti");
        }
    }
}
