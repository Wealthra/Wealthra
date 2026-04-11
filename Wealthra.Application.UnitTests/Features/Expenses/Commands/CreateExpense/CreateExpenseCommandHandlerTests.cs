using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Expenses.Commands.CreateExpense;
using Wealthra.Application.Features.Recommendations.Commands.AnalyzeSpendingAnomalies;
using Wealthra.Domain.Entities;
using Wealthra.Domain.Common;
using MockQueryable.Moq;
using MediatR;
using System.Collections.Generic;
using System.Reflection;

namespace Wealthra.Application.UnitTests.Features.Expenses.Commands.CreateExpense;

public class CreateExpenseCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockICurrentUserService;
    private readonly Mock<ISender> _mockSender;

    private readonly Mock<ICurrencyExchangeService> _mockCurrencyService;
    private readonly CreateExpenseCommandHandler _handler;

    public CreateExpenseCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockICurrentUserService = new Mock<ICurrentUserService>();
        _mockSender = new Mock<ISender>();

        _mockCurrencyService = new Mock<ICurrencyExchangeService>();

        _handler = new CreateExpenseCommandHandler(_mockContext.Object, _mockICurrentUserService.Object, _mockSender.Object, _mockCurrencyService.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_ShouldTriggerAnomalyAnalysis()
    {
        // Arrange
        var userId = "test-user-id";
        _mockICurrentUserService.Setup(x => x.UserId).Returns(userId);
        
        var categoryId = 1;
        var category = new Category("Food", "Gıda");
        typeof(BaseEntity).GetProperty("Id")!.SetValue(category, categoryId);

        _mockContext.Setup(x => x.Categories)
            .Returns(new List<Category> { category }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Budgets)
            .Returns(new List<Budget>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Expenses)
            .Returns(new List<Expense>().BuildMockDbSet().Object);

        var request = new CreateExpenseCommand
        {
            Description = "Test Expense",
            Amount = 500,
            CategoryId = categoryId,
            TransactionDate = new DateTime(2026, 3, 7),
            PaymentMethod = "Cash"
        };

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        
        _mockSender.Verify(x => x.Send(
            It.Is<AnalyzeSpendingAnomaliesCommand>(c => c.Year == 2026 && c.Month == 3),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
