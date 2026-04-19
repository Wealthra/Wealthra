using FluentAssertions;
using MediatR;
using Moq;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Identity.Commands.ForgotPassword;
using Xunit;

namespace Wealthra.Application.UnitTests.Features.Identity.Commands.ForgotPassword;

public class ForgotPasswordCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnWithoutEmail_WhenUserDoesNotExist()
    {
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(x => x.GeneratePasswordResetTokenAsync(It.IsAny<string>()))
            .ReturnsAsync((false, string.Empty, string.Empty));

        var cacheService = new Mock<ICacheService>();
        var emailSender = new Mock<IEmailSender>();
        var realtime = new Mock<IAdminRealtimeService>();
        var handler = new ForgotPasswordCommandHandler(identityService.Object, cacheService.Object, emailSender.Object, realtime.Object);

        var result = await handler.Handle(new ForgotPasswordCommand("unknown@wealthra.local"), CancellationToken.None);

        result.Should().Be(Unit.Value);
        emailSender.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
