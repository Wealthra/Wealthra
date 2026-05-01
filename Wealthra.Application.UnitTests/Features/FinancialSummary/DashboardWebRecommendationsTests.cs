using FluentAssertions;
using Wealthra.Application.Features.FinancialSummary;
using Wealthra.Application.Features.FinancialSummary.Models;
using Xunit;

namespace Wealthra.Application.UnitTests.Features.FinancialSummary;

public class DashboardWebRecommendationsTests
{
    [Theory]
    [InlineData(0.42, "0.42")]
    [InlineData(0.004, "0.00")]
    [InlineData(0.005, "0.01")]
    [InlineData(1234.567, "1234.57")]
    public void FormatMoneyForDisplay_UsesInvariantTwoDecimals(decimal amount, string expected)
    {
        DashboardWebRecommendations.FormatMoneyForDisplay(amount).Should().Be(expected);
    }

    [Fact]
    public void BuildFromBudgetAlerts_ExceededWithSubOneOver_ShowsDecimalNotZero()
    {
        var alerts = new List<DashboardWebBudgetAlertDto>
        {
            new(
                BudgetId: 1,
                CategoryName: "Food",
                LimitAmount: 100m,
                CurrentAmount: 100.42m,
                PercentageUsed: 1.0042m,
                Status: "Exceeded")
        };

        var result = DashboardWebRecommendations.BuildFromBudgetAlerts(alerts, "EUR");

        result.Should().ContainSingle();
        result[0].Description.Should().Contain("0.42 EUR").And.NotContain("0 EUR over");
    }

    [Fact]
    public void BuildFromBudgetAlerts_WarningWithSubOneRoom_ShowsDecimalNotZero()
    {
        var alerts = new List<DashboardWebBudgetAlertDto>
        {
            new(
                BudgetId: 2,
                CategoryName: "Travel",
                LimitAmount: 100m,
                CurrentAmount: 99.58m,
                PercentageUsed: 0.9958m,
                Status: "Warning")
        };

        var result = DashboardWebRecommendations.BuildFromBudgetAlerts(alerts, "EUR");

        result.Should().ContainSingle();
        result[0].Description.Should().Contain("0.42 EUR");
    }
}
