using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Infrastructure.Settings;

namespace Wealthra.Infrastructure.Services
{
    public class EmailService : IEmailSender
    {
        private readonly SmtpOptions _smtpOptions;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<SmtpOptions> smtpOptions, ILogger<EmailService> logger)
        {
            _smtpOptions = smtpOptions.Value;
            _logger = logger;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_smtpOptions.Host);

        public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_smtpOptions.Host))
            {
                _logger.LogWarning("SMTP Host is not configured; skipping send to {Recipient}", to);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtpOptions.FromName, _smtpOptions.FromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = body };

            var secure = ResolveSecureSocketOptions(_smtpOptions);

            try
            {
                using var smtpClient = new SmtpClient();
                await smtpClient.ConnectAsync(
                    _smtpOptions.Host,
                    _smtpOptions.Port,
                    secure,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(_smtpOptions.Username))
                {
                    await smtpClient.AuthenticateAsync(_smtpOptions.Username, _smtpOptions.Password, cancellationToken);
                }

                await smtpClient.SendAsync(message, cancellationToken);
                await smtpClient.DisconnectAsync(true, cancellationToken);
                _logger.LogDebug("SMTP message sent to {Recipient} (subject: {Subject})", to, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP send failed for {Recipient} (subject: {Subject})", to, subject);
                throw;
            }
        }

        private static SecureSocketOptions ResolveSecureSocketOptions(SmtpOptions options)
        {
            var mode = options.Tls?.Trim().ToLowerInvariant();
            return mode switch
            {
                "none" => SecureSocketOptions.None,
                "starttls" => SecureSocketOptions.StartTls,
                "ssl" => SecureSocketOptions.SslOnConnect,
                "auto" => SecureSocketOptions.Auto,
                _ => options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls
            };
        }
    }
}