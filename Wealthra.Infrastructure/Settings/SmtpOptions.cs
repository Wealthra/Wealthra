namespace Wealthra.Infrastructure.Settings;

public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;

    /// <summary>
    /// Legacy toggle: when <see cref="Tls"/> is empty, true maps to implicit TLS (port 465),
    /// false maps to STARTTLS (typical for port 587).
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Optional override: None, StartTls, Ssl, Auto. When null/empty, <see cref="UseSsl"/> decides.
    /// </summary>
    public string? Tls { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Wealthra";
}
