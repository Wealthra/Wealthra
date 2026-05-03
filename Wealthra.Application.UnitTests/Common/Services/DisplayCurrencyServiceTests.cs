using FluentAssertions;
using Moq;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Models;
using Wealthra.Infrastructure.Services;

namespace Wealthra.Application.UnitTests.Common.Services;

public class DisplayCurrencyServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IIdentityService> _identity = new();

    private DisplayCurrencyService CreateSut() => new(_currentUser.Object, _identity.Object);

    [Fact]
    public async Task GetEffectiveCurrencyAsync_WhenRequestProvided_ReturnsUppercaseOverride()
    {
        _currentUser.Setup(x => x.UserId).Returns("u1");
        var sut = CreateSut();

        var code = await sut.GetEffectiveCurrencyAsync(" usd ");

        code.Should().Be("USD");
        _identity.Verify(x => x.GetUserDetailsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetEffectiveCurrencyAsync_WhenRequestEmpty_UsesPreferredCurrency()
    {
        _currentUser.Setup(x => x.UserId).Returns("u1");
        _identity.Setup(x => x.GetUserDetailsAsync("u1"))
            .ReturnsAsync(new UserDto("u1", "a@b.com", "A", "B", null, DateTime.UtcNow, "eur", false));

        var sut = CreateSut();

        var code = await sut.GetEffectiveCurrencyAsync(null);

        code.Should().Be("EUR");
    }

    [Fact]
    public async Task GetEffectiveCurrencyAsync_WhenNoUser_FallsBackToTry()
    {
        _currentUser.Setup(x => x.UserId).Returns((string?)null);

        var sut = CreateSut();

        var code = await sut.GetEffectiveCurrencyAsync(null);

        code.Should().Be("TRY");
        _identity.Verify(x => x.GetUserDetailsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetEffectiveCurrencyAsync_WhenPreferredMissing_FallsBackToTry()
    {
        _currentUser.Setup(x => x.UserId).Returns("u1");
        _identity.Setup(x => x.GetUserDetailsAsync("u1"))
            .ReturnsAsync(new UserDto("u1", "a@b.com", "A", "B", null, DateTime.UtcNow, "", false));

        var sut = CreateSut();

        var code = await sut.GetEffectiveCurrencyAsync("  ");

        code.Should().Be("TRY");
    }
}
