using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Interfaces;
using AppValidationException = Wealthra.Application.Common.Exceptions.ValidationException;

namespace Wealthra.Application.Features.Admin.Commands.SendTestSmtpEmail;

public record SendTestSmtpEmailCommand(string To) : IRequest<SendTestSmtpEmailResult>;

public record SendTestSmtpEmailResult(string Message);

public class SendTestSmtpEmailCommandValidator : AbstractValidator<SendTestSmtpEmailCommand>
{
    public SendTestSmtpEmailCommandValidator()
    {
        RuleFor(x => x.To).NotEmpty().EmailAddress();
    }
}

public class SendTestSmtpEmailCommandHandler : IRequestHandler<SendTestSmtpEmailCommand, SendTestSmtpEmailResult>
{
    private readonly IEmailSender _emailSender;

    public SendTestSmtpEmailCommandHandler(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public async Task<SendTestSmtpEmailResult> Handle(SendTestSmtpEmailCommand request, CancellationToken cancellationToken)
    {
        if (!_emailSender.IsConfigured)
        {
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["smtp"] = ["SMTP is not configured. Set Smtp:Host (and credentials) in configuration."]
            });
        }

        var to = request.To.Trim();
        const string subject = "Wealthra SMTP test";
        var body = $"""
            <p>This is a test message from the Wealthra API.</p>
            <p>If you received this, outbound SMTP is working.</p>
            <p><small>Sent at {DateTime.UtcNow:O} UTC</small></p>
            """;

        await _emailSender.SendEmailAsync(to, subject, body, cancellationToken);
        return new SendTestSmtpEmailResult($"Test email sent to {to}.");
    }
}
