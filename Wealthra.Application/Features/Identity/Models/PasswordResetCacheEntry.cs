namespace Wealthra.Application.Features.Identity.Models;

public class PasswordResetCacheEntry
{
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset ExpiresOn { get; set; }
}
