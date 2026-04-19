using FluentAssertions;
using Moq;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;
using Wealthra.Application.Features.Admin.Queries.GetAppUsageSummary;
using Xunit;

namespace Wealthra.Application.UnitTests.Features.Admin.Queries.GetAppUsageSummary;

public class GetAppUsageSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnSummaryFromIdentityService()
    {
        var identityService = new Mock<IIdentityService>();
        var expected = new AppUsageSummaryDto(
            5,
            2,
            44,
            31,
            [new PlanUsageBreakdownDto(1, "Basic", 5, 44, 31)]);

        identityService.Setup(x => x.GetAppUsageSummaryAsync()).ReturnsAsync(expected);
        var handler = new GetAppUsageSummaryQueryHandler(identityService.Object);

        var result = await handler.Handle(new GetAppUsageSummaryQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }
}
