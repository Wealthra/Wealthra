using FluentAssertions;
using Moq;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Commands.SendTestSmtpEmail;
using Xunit;

namespace Wealthra.Application.UnitTests.Features.Admin.Commands.SendTestSmtpEmail;

public class SendTestSmtpEmailCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_ThrowValidation_WhenSmtpNotConfigured()
    {
        var email = new Mock<IEmailSender>();
        email.SetupGet(x => x.IsConfigured).Returns(false);
        var handler = new SendTestSmtpEmailCommandHandler(email.Object);

        var act = async () => await handler.Handle(new SendTestSmtpEmailCommand("a@b.com"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        email.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_SendAndReturnMessage_WhenConfigured()
    {
        var email = new Mock<IEmailSender>();
        email.SetupGet(x => x.IsConfigured).Returns(true);
        var handler = new SendTestSmtpEmailCommandHandler(email.Object);

        var result = await handler.Handle(new SendTestSmtpEmailCommand("  user@example.com  "), CancellationToken.None);

        result.Message.Should().Contain("user@example.com");
        email.Verify(
            x => x.SendEmailAsync(
                "user@example.com",
                It.Is<string>(s => s.Contains("SMTP test", StringComparison.OrdinalIgnoreCase)),
                It.Is<string>(b => b.Contains("Wealthra", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
