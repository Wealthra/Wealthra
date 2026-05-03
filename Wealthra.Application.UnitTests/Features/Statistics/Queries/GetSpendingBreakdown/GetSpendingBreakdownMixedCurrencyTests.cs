using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Statistics.Queries.GetSpendingBreakdown;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.UnitTests.Features.Statistics.Queries.GetSpendingBreakdown;

public class GetSpendingBreakdownMixedCurrencyTests
{
    [Fact]
    public async Task Handle_ConvertsEachExpense_ToEffectiveCurrencyBeforeTotals()
    {
        var userId = "user-1";
        var mockContext = new Mock<IApplicationDbContext>();
        var mockUser = new Mock<ICurrentUserService>();
        var mockFx = new Mock<ICurrencyExchangeService>();
        var mockDisplay = new Mock<IDisplayCurrencyService>();

        mockUser.Setup(x => x.UserId).Returns(userId);
        mockDisplay.Setup(x => x.GetEffectiveCurrencyAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("TRY");

        var cat = new Category("Food", "Gida");
        var day = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        var expenses = new List<Expense>
        {
            new()
            {
                CreatedBy = userId,
                CategoryId = 1,
                Category = cat,
                Amount = 40m,
                Currency = "TRY",
                TransactionDate = day,
                Description = "a"
            },
            new()
            {
                CreatedBy = userId,
                CategoryId = 1,
                Category = cat,
                Amount = 10m,
                Currency = "EUR",
                TransactionDate = day,
                Description = "b"
            }
        };

        mockContext.Setup(x => x.Expenses).Returns(expenses.BuildMockDbSet().Object);

        mockFx.Setup(x => x.ConvertAsync(10m, "EUR", "TRY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(350m);

        var handler = new GetSpendingBreakdownQueryHandler(
            mockContext.Object,
            mockUser.Object,
            mockFx.Object,
            mockDisplay.Object);

        var result = await handler.Handle(
            new GetSpendingBreakdownQuery { StartDate = day.AddDays(-1), EndDate = day.AddDays(1) },
            CancellationToken.None);

        result.Currency.Should().Be("TRY");
        result.TotalAmount.Should().Be(390m);
        result.CategoryBreakdown.Should().ContainSingle();
        result.CategoryBreakdown[0].Amount.Should().Be(390m);
    }
}
