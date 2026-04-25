using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Models;
using Wealthra.Application.Features.Recommendations.Models;
using Wealthra.Application.Features.Recommendations.Queries.GetPersonalizedRecommendations;
using Wealthra.Domain.Entities;
using Wealthra.Domain.Enums;

namespace Wealthra.Application.UnitTests.Features.Recommendations.Queries.GetPersonalizedRecommendations
{
    public class GetPersonalizedRecommendationsQueryHandlerTests
    {
        private readonly Mock<IApplicationDbContext> _contextMock = new();
        private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
        private readonly Mock<IHeuristicRecommendationService> _heuristicServiceMock = new();
        private readonly Mock<ICollaborativeRecommendationService> _collaborativeServiceMock = new();
        private readonly Mock<ISemanticTipRecommendationService> _semanticServiceMock = new();
        private readonly Mock<IRecommendationFeatureFlags> _featureFlagsMock = new();
        private readonly Mock<IIdentityService> _identityServiceMock = new();

        private readonly string _userId = "test-user";
        private readonly DateTime _targetMonth = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        public GetPersonalizedRecommendationsQueryHandlerTests()
        {
            _currentUserServiceMock.Setup(x => x.UserId).Returns(_userId);
            _featureFlagsMock.SetupGet(x => x.EnableCollaborativeFiltering).Returns(true);
            _featureFlagsMock.SetupGet(x => x.EnableSemanticTips).Returns(true);

            var metrics = new List<MonthlyCategoryMetric>
            {
                new()
                {
                    UserId = _userId,
                    Month = _targetMonth,
                    CategoryId = 1,
                    CategoryName = "Food",
                    CategoryNameTr = "Gida",
                    TotalSpend = 1000,
                    TotalIncome = 5000,
                    SpendPercentageOfIncome = 20,
                    PreviousMonthSpend = 700
                }
            };
            _contextMock.Setup(x => x.MonthlyCategoryMetrics).Returns(metrics.BuildMockDbSet().Object);

            _heuristicServiceMock.Setup(x => x.Evaluate(It.IsAny<IReadOnlyCollection<MonthlyCategoryMetric>>()))
                .Returns(new List<RecommendationSignal>
                {
                    new()
                    {
                        Source = "heuristic",
                        Severity = "high",
                        ReasonCode = "HIGH_INCOME_SHARE",
                        Evidence = "evidence",
                        CategoryId = 1,
                        CategoryName = "Food"
                    }
                });

            _collaborativeServiceMock
                .Setup(x => x.GetSuggestionsAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CollaborativeSuggestion> { new() { CategoryId = 2, CategoryName = "Invest", Score = 0.7f } });

            _semanticServiceMock
                .Setup(x => x.GetTipsAsync(_userId, It.IsAny<RecommendationSignal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SemanticTipResult> { new() { TipId = 1, Topic = "Tip", Body = "Body" } });
        }

        [Fact]
        public async Task Handle_FreeTier_ShouldReturnOnlyLayer1()
        {
            _identityServiceMock
                .Setup(x => x.GetUserUsageAsync(_userId))
                .ReturnsAsync(new UserUsageDto(_userId, "a@b.com", "A", "B", SubscriptionTier.Free, null, null, 0, 0, null));

            var handler = BuildHandler();
            var result = await handler.Handle(new GetPersonalizedRecommendationsQuery { Year = 2026, Month = 4 }, CancellationToken.None);

            result.Signals.Should().HaveCount(1);
            result.CollaborativeSuggestions.Should().BeEmpty();
            result.SemanticTips.Should().BeEmpty();
            _collaborativeServiceMock.Verify(x => x.GetSuggestionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _semanticServiceMock.Verify(x => x.GetTipsAsync(It.IsAny<string>(), It.IsAny<RecommendationSignal>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_BasicTier_ShouldReturnLayer1AndLayer2()
        {
            _identityServiceMock
                .Setup(x => x.GetUserUsageAsync(_userId))
                .ReturnsAsync(new UserUsageDto(_userId, "a@b.com", "A", "B", SubscriptionTier.Basic, null, null, 0, 0, null));

            var handler = BuildHandler();
            var result = await handler.Handle(new GetPersonalizedRecommendationsQuery { Year = 2026, Month = 4 }, CancellationToken.None);

            result.Signals.Should().HaveCount(1);
            result.CollaborativeSuggestions.Should().HaveCount(1);
            result.SemanticTips.Should().BeEmpty();
            _collaborativeServiceMock.Verify(x => x.GetSuggestionsAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
            _semanticServiceMock.Verify(x => x.GetTipsAsync(It.IsAny<string>(), It.IsAny<RecommendationSignal>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_LimitlessTier_ShouldReturnAllLayers()
        {
            _identityServiceMock
                .Setup(x => x.GetUserUsageAsync(_userId))
                .ReturnsAsync(new UserUsageDto(_userId, "a@b.com", "A", "B", SubscriptionTier.Limitless, null, null, 0, 0, null));

            var handler = BuildHandler();
            var result = await handler.Handle(new GetPersonalizedRecommendationsQuery { Year = 2026, Month = 4 }, CancellationToken.None);

            result.Signals.Should().HaveCount(1);
            result.CollaborativeSuggestions.Should().HaveCount(1);
            result.SemanticTips.Should().HaveCount(1);
            _collaborativeServiceMock.Verify(x => x.GetSuggestionsAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
            _semanticServiceMock.Verify(x => x.GetTipsAsync(_userId, It.IsAny<RecommendationSignal>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private GetPersonalizedRecommendationsQueryHandler BuildHandler()
        {
            return new GetPersonalizedRecommendationsQueryHandler(
                _contextMock.Object,
                _currentUserServiceMock.Object,
                _heuristicServiceMock.Object,
                _collaborativeServiceMock.Object,
                _semanticServiceMock.Object,
                _featureFlagsMock.Object,
                _identityServiceMock.Object);
        }
    }
}
