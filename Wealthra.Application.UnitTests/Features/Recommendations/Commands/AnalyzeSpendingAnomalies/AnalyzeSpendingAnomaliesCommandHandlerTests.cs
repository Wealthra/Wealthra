using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Xunit;
using FluentAssertions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Commands.AnalyzeSpendingAnomalies;
using Wealthra.Application.Features.Recommendations.Services;
using Wealthra.Domain.Entities;
using Wealthra.Domain.Enums;
using MockQueryable.Moq;
using System.Linq;

namespace Wealthra.Application.UnitTests.Features.Recommendations.Commands.AnalyzeSpendingAnomalies
{
    public class AnalyzeSpendingAnomaliesCommandHandlerTests
    {
        private readonly Mock<IApplicationDbContext> _contextMock;
        private readonly Mock<ICurrentUserService> _currentUserServiceMock;
        private readonly IHeuristicRecommendationService _heuristicRecommendationService;
        private readonly AnalyzeSpendingAnomaliesCommandHandler _handler;

        public AnalyzeSpendingAnomaliesCommandHandlerTests()
        {
            _contextMock = new Mock<IApplicationDbContext>();
            _currentUserServiceMock = new Mock<ICurrentUserService>();
            _heuristicRecommendationService = new HeuristicRecommendationService();

            _handler = new AnalyzeSpendingAnomaliesCommandHandler(
                _contextMock.Object,
                _currentUserServiceMock.Object,
                _heuristicRecommendationService);
        }

        [Fact]
        public async Task Handle_HighPercentageOfIncome_ShouldCreateAlertNotification()
        {
            // Arrange
            var userId = "test-user-id";
            var targetYear = 2026;
            var targetMonth = 3;
            var targetDate = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            
            _currentUserServiceMock.Setup(x => x.UserId).Returns(userId);

            var metric = new MonthlyCategoryMetric
            {
                UserId = userId,
                Month = targetDate,
                CategoryId = 1,
                CategoryName = "Dining Out",
                TotalSpend = 1500,
                TotalIncome = 4000,
                SpendPercentageOfIncome = 37.5m, // > 30%
                PreviousMonthSpend = 1400
            };

            var metrics = new List<MonthlyCategoryMetric> { metric }.BuildMockDbSet();
            _contextMock.Setup(x => x.MonthlyCategoryMetrics).Returns(metrics.Object);
            
            var notifications = new List<Notification>();
            var mockNotificationsDbSet = new List<Notification>().BuildMockDbSet();
            _contextMock.Setup(x => x.Notifications).Returns(mockNotificationsDbSet.Object);
            _contextMock.Setup(x => x.Notifications.Add(It.IsAny<Notification>())).Callback<Notification>(n => notifications.Add(n));

            var command = new AnalyzeSpendingAnomaliesCommand { Year = targetYear, Month = targetMonth };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().HaveCount(1);
            result[0].Should().Contain("Dining Out").And.Contain("%37.5");
            
            _contextMock.Verify(x => x.Notifications.Add(It.IsAny<Notification>()), Times.Once);
            notifications.Should().HaveCount(1);
            notifications[0].UserId.Should().Be(userId);
            notifications[0].Type.Should().Be(NotificationType.Alert);
            notifications[0].MessageEn.Should().Contain("Dining Out");
            notifications[0].MessageTr.Should().Contain("Dining Out");
            
            _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_SpikeInSpending_ShouldCreateAlertNotification()
        {
            // Arrange
            var userId = "test-user-id";
            var targetYear = 2026;
            var targetMonth = 3;
            var targetDate = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            
            _currentUserServiceMock.Setup(x => x.UserId).Returns(userId);

            var metric = new MonthlyCategoryMetric
            {
                UserId = userId,
                Month = targetDate,
                CategoryId = 2,
                CategoryName = "Shopping",
                TotalSpend = 1600,
                TotalIncome = 10000,
                SpendPercentageOfIncome = 16m, // < 30%
                PreviousMonthSpend = 1000 // 160% increase (>1.5 ratio)
            };

            var metrics = new List<MonthlyCategoryMetric> { metric }.BuildMockDbSet();
            _contextMock.Setup(x => x.MonthlyCategoryMetrics).Returns(metrics.Object);
            
            var notifications = new List<Notification>();
            var mockNotificationsDbSet = new List<Notification>().BuildMockDbSet();
            _contextMock.Setup(x => x.Notifications).Returns(mockNotificationsDbSet.Object);
            _contextMock.Setup(x => x.Notifications.Add(It.IsAny<Notification>())).Callback<Notification>(n => notifications.Add(n));

            var command = new AnalyzeSpendingAnomaliesCommand { Year = targetYear, Month = targetMonth };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().HaveCount(1);
            result[0].Should().Contain("Shopping").And.Contain("%60");
            
            _contextMock.Verify(x => x.Notifications.Add(It.IsAny<Notification>()), Times.Once);
            notifications.Should().HaveCount(1);
            notifications[0].UserId.Should().Be(userId);
            notifications[0].Type.Should().Be(NotificationType.Alert);
            
            _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_NormalSpending_ShouldNotCreateAlerts()
        {
            // Arrange
            var userId = "test-user-id";
            var targetYear = 2026;
            var targetMonth = 3;
            var targetDate = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            
            _currentUserServiceMock.Setup(x => x.UserId).Returns(userId);

            var metric = new MonthlyCategoryMetric
            {
                UserId = userId,
                Month = targetDate,
                CategoryId = 3,
                CategoryName = "Groceries",
                TotalSpend = 500,
                TotalIncome = 5000,
                SpendPercentageOfIncome = 10m, // < 30%
                PreviousMonthSpend = 480 // < 150% increase
            };

            var metrics = new List<MonthlyCategoryMetric> { metric }.BuildMockDbSet();
            _contextMock.Setup(x => x.MonthlyCategoryMetrics).Returns(metrics.Object);
            
            var notifications = new List<Notification>();
            var mockNotificationsDbSet = new List<Notification>().BuildMockDbSet();
            _contextMock.Setup(x => x.Notifications).Returns(mockNotificationsDbSet.Object);
            _contextMock.Setup(x => x.Notifications.Add(It.IsAny<Notification>())).Callback<Notification>(n => notifications.Add(n));

            var command = new AnalyzeSpendingAnomaliesCommand { Year = targetYear, Month = targetMonth };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().BeEmpty();
            _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_AnomalousSpendButAlreadyNotified_ShouldNotCreateDuplicateNotification()
        {
            // Arrange
            var userId = "test-user-id";
            var targetYear = 2026;
            var targetMonth = 3;
            var targetDate = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            
            _currentUserServiceMock.Setup(x => x.UserId).Returns(userId);

            var metric = new MonthlyCategoryMetric
            {
                UserId = userId,
                Month = targetDate,
                CategoryId = 1,
                CategoryName = "Dining Out",
                TotalSpend = 1500,
                TotalIncome = 4000,
                SpendPercentageOfIncome = 37.5m, // > 30%
                PreviousMonthSpend = 1400
            };

            var metrics = new List<MonthlyCategoryMetric> { metric }.BuildMockDbSet();
            _contextMock.Setup(x => x.MonthlyCategoryMetrics).Returns(metrics.Object);
            
            // Existing notification in DB for this month/category
            var existingNotification = new Notification
            {
                UserId = userId,
                RelatedEntityId = 1,
                MessageEn = "Alert: Dining Out [HIGH_INCOME_SHARE]",
                MessageTr = "Uyarı: Dining Out toplam gelirinizin %",
                Type = NotificationType.Alert,
                CreatedOn = new DateTime(targetYear, targetMonth, 2)
            };
            
            var notificationsDbSet = new List<Notification> { existingNotification }.BuildMockDbSet();
            _contextMock.Setup(x => x.Notifications).Returns(notificationsDbSet.Object);

            var command = new AnalyzeSpendingAnomaliesCommand { Year = targetYear, Month = targetMonth };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().ContainSingle().Which.Should().Contain("HIGH_INCOME_SHARE");
            _contextMock.Verify(x => x.Notifications.Add(It.IsAny<Notification>()), Times.Never);
            _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
