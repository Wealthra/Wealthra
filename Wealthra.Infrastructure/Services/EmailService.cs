using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Infrastructure.Settings;

namespace Wealthra.Infrastructure.Services
{
    public class EmailService : IEmailSender
    {
        private readonly SmtpOptions _smtpOptions;

        public EmailService(IOptions<SmtpOptions> smtpOptions)
        {
            _smtpOptions = smtpOptions.Value;
        }

        public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_smtpOptions.Host))
            {
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtpOptions.FromName, _smtpOptions.FromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = body };

            using var smtpClient = new SmtpClient();
            await smtpClient.ConnectAsync(
                _smtpOptions.Host,
                _smtpOptions.Port,
                _smtpOptions.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_smtpOptions.Username))
            {
                await smtpClient.AuthenticateAsync(_smtpOptions.Username, _smtpOptions.Password, cancellationToken);
            }

            await smtpClient.SendAsync(message, cancellationToken);
            await smtpClient.DisconnectAsync(true, cancellationToken);
        }
    }
}