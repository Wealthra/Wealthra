namespace Wealthra.Application.Common.Interfaces;

public interface IEmailSender
{
    /// <summary>False when SMTP host is not set; <see cref="SendEmailAsync"/> becomes a no-op.</summary>
    bool IsConfigured { get; }

    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
