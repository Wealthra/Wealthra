using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Xunit;
using FluentAssertions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Commands.DeleteAccount;
using Wealthra.Domain.Entities;
using Wealthra.Domain.Common;
using Wealthra.Application.Common.Models;
using MockQueryable.Moq;
using System.Linq;

namespace Wealthra.Application.UnitTests.Features.Identity.Commands.DeleteAccount
{
    public class DeleteAccountCommandHandlerTests
    {
        private readonly Mock<IApplicationDbContext> _contextMock;
        private readonly Mock<IIdentityService> _identityServiceMock;
        private readonly Mock<ICurrentUserService> _currentUserServiceMock;
        private readonly DeleteAccountCommandHandler _handler;

        public DeleteAccountCommandHandlerTests()
        {
            _contextMock = new Mock<IApplicationDbContext>();
            _identityServiceMock = new Mock<IIdentityService>();
            _currentUserServiceMock = new Mock<ICurrentUserService>();

            _handler = new DeleteAccountCommandHandler(
                _contextMock.Object,
                _identityServiceMock.Object,
                _currentUserServiceMock.Object);
        }

        [Fact]
        public async Task Handle_ValidUser_ShouldDeleteAllUserData()
        {
            // Arrange
            var userId = "test-user-id";
            _currentUserServiceMock.Setup(x => x.UserId).Returns(userId);

            var budgets = new List<Budget> { new Budget(1, 100) { CreatedBy = userId } }.BuildMockDbSet();
            var expenses = new List<Expense> { new Expense { CreatedBy = userId } }.BuildMockDbSet();
            var incomes = new List<Income> { new Income { CreatedBy = userId } }.BuildMockDbSet();
            var goals = new List<Goal> { new Goal { CreatedBy = userId } }.BuildMockDbSet();
            var notifications = new List<Notification> { new Notification { UserId = userId } }.BuildMockDbSet();
            
            _contextMock.Setup(x => x.Budgets).Returns(budgets.Object);
            _contextMock.Setup(x => x.Expenses).Returns(expenses.Object);
            _contextMock.Setup(x => x.Incomes).Returns(incomes.Object);
            _contextMock.Setup(x => x.Goals).Returns(goals.Object);
            _contextMock.Setup(x => x.Notifications).Returns(notifications.Object);

            _identityServiceMock.Setup(x => x.DeleteUserAsync(userId))
                .ReturnsAsync(Result.Success());

            // Act
            var result = await _handler.Handle(new DeleteAccountCommand(), CancellationToken.None);

            // Assert
            _contextMock.Verify(x => x.Budgets.RemoveRange(It.IsAny<IEnumerable<Budget>>()), Times.Once);
            _contextMock.Verify(x => x.Expenses.RemoveRange(It.IsAny<IEnumerable<Expense>>()), Times.Once);
            _contextMock.Verify(x => x.Incomes.RemoveRange(It.IsAny<IEnumerable<Income>>()), Times.Once);
            _contextMock.Verify(x => x.Goals.RemoveRange(It.IsAny<IEnumerable<Goal>>()), Times.Once);
            _contextMock.Verify(x => x.Notifications.RemoveRange(It.IsAny<IEnumerable<Notification>>()), Times.Once);
            
            _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _identityServiceMock.Verify(x => x.DeleteUserAsync(userId), Times.Once);

            result.Should().Be(Unit.Value);
        }

        [Fact]
        public async Task Handle_UnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            _currentUserServiceMock.Setup(x => x.UserId).Returns((string)null);

            // Act
            Func<Task> act = async () => await _handler.Handle(new DeleteAccountCommand(), CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task Handle_IdentityServiceFails_ShouldThrowException()
        {
            // Arrange
            var userId = "test-user-id";
            _currentUserServiceMock.Setup(x => x.UserId).Returns(userId);

            _contextMock.Setup(x => x.Budgets).Returns(new List<Budget>().BuildMockDbSet().Object);
            _contextMock.Setup(x => x.Expenses).Returns(new List<Expense>().BuildMockDbSet().Object);
            _contextMock.Setup(x => x.Incomes).Returns(new List<Income>().BuildMockDbSet().Object);
            _contextMock.Setup(x => x.Goals).Returns(new List<Goal>().BuildMockDbSet().Object);
            _contextMock.Setup(x => x.Notifications).Returns(new List<Notification>().BuildMockDbSet().Object);

            _identityServiceMock.Setup(x => x.DeleteUserAsync(userId))
                .ReturnsAsync(Result.Failure(new[] { "User not found" }));

            // Act
            Func<Task> act = async () => await _handler.Handle(new DeleteAccountCommand(), CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("*Failed to delete user account*");
        }
    }
}
